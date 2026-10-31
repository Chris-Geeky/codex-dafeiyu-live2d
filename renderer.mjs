/**
 * Browser-side Live2D adapter for the official Codex pet avatar.
 *
 * The exported function is deliberately self-contained because the CLI
 * serialises it with Function#toString and evaluates it through CDP. Keep all
 * implementation details inside this function: it may not rely on imports or
 * module-scope bindings.
 */
export async function installCodexLive2D(config) {
  const globalScope = globalThis
  const documentScope = globalScope.document
  const ROOT_SELECTOR = ".codex-avatar-root[data-codex-pet-id][data-codex-pet-state]"
  const STYLE_ID = "codex-live2d-renderer-style"
  const RENDERED_ATTRIBUTE = "data-codex-live2d-rendered"
  const MARKER_TOKEN = [
    "codex-live2d",
    Date.now().toString(36),
    Math.random().toString(36).slice(2),
  ].join("-")
  const VENDOR_READY = Symbol.for("codex-live2d.renderer.vendor-ready")
  const STATE_ATTRIBUTE = "data-codex-pet-state"
  const PET_ID_ATTRIBUTE = "data-codex-pet-id"
  const KNOWN_STATES = new Set([
    "idle",
    "running",
    "waiting",
    "review",
    "failed",
    "waving",
    "jumping",
    "running-left",
    "running-right",
  ])
  const STATE_EXPRESSIONS = Object.freeze({
    idle: ["idle", "neutral", "normal", "平静"],
    running: ["running", "focus", "专注"],
    "running-left": ["running", "focus", "专注"],
    "running-right": ["running", "focus", "专注"],
    waiting: ["waiting", "question", "问号", "sweat", "流汗"],
    review: ["review", "sparkle", "情绪花花", "star-eyes", "星星眼"],
    failed: ["failed", "sad", "悲伤", "cry", "大哭", "angry", "生气"],
    waving: ["waving", "excited", "开心兴奋", "heart-pop", "冒爱心"],
    jumping: ["jumping", "excited", "开心兴奋", "star-eyes", "星星眼"],
  })

  const previous = globalScope.__codexLive2D
  if (previous && typeof previous.dispose === "function") {
    try {
      previous.dispose()
    } catch {
      // A failed prior instance must not prevent a fresh installation.
    }
  }
  documentScope?.getElementById(STYLE_ID)?.remove()

  const supplied = config && typeof config === "object" ? config : {}
  const petId = supplied.petId === null || supplied.petId === undefined
    ? null
    : String(supplied.petId)
  const allowAnyPet = supplied.allowAnyPet === true
  let configuredFps = supplied.fps === 60 ? 60 : 30
  const bundle = supplied.bundle
  const appearanceCatalog = bundle?.appearanceCatalog || {expressions: [{id: "auto"}, {id: "none"}], groups: [], automaticExpressions: []}
  function normalizeAppearance(value = {}) {
    if (!value || typeof value !== "object" || Array.isArray(value)) throw new TypeError("Invalid appearance settings")
    if (Object.keys(value).some(key => !["expression", "autoExpressions", "accessories"].includes(key))) throw new RangeError("Unknown appearance setting")
    const expression = value.expression ?? "auto", autoExpressions = value.autoExpressions ?? true, choices = value.accessories ?? {}
    if (!appearanceCatalog.expressions.some(item => item.id === expression) || typeof autoExpressions !== "boolean") throw new RangeError("Unknown expression choice")
    if (!choices || typeof choices !== "object" || Array.isArray(choices) || Object.keys(choices).some(key => !appearanceCatalog.groups.some(group => group.id === key))) throw new RangeError("Unknown accessory group")
    const accessories = Object.fromEntries(appearanceCatalog.groups.map(group => {
      const choice = choices[group.id] ?? "none"
      if (!group.options.some(option => option.id === choice)) throw new RangeError("Unknown accessory choice")
      return [group.id, choice]
    }))
    return {expression, autoExpressions, accessories}
  }
  let appearance = normalizeAppearance(supplied.appearance)

  let disposed = false
  let bootAttempted = false
  let bootPromise = null
  let active = false
  let modelReady = false
  let renderedOnce = false
  let playbackRunning = false
  let lastError = null
  let lastWarning = null
  let bootStage = "preparing"
  let frames = 0
  let state = "idle"
  let forcedState = null
  let lastNativeState = null
  let root = null
  let rootVisible = true
  let canvas = null
  let app = null
  let model = null
  let blobUrls = []
  let rootLogicalWidth = 1
  let rootLogicalHeight = 1
  let sourceSize = null
  let currentExpressionName = null
  let expressionQueue = Promise.resolve()
  let documentObserver = null
  let rootObserver = null
  let resizeObserver = null
  let intersectionObserver = null
  let reconcileQueued = false
  let markerRoot = null
  let styleElement = null
  let contextLostHandler = null
  let motionPreference = null
  let motionPreferenceHandler = null
  let motionDirector = null
  let motionExpression = null
  let idleExpression = null
  let appearanceLayer = null
  let appearanceHitRefresh = -1
  let previewPlaying = true, previewFrames = 0, desktopEffect = null
  let hitMask = []
  let hitLayer = null
  let hitHosts = []
  let hitAreaError = null
  let controlsHover = null

  function canvasMetrics() {
    const paddingX = Math.ceil(rootLogicalWidth * 0.35)
    const paddingY = Math.ceil(rootLogicalHeight * 0.35)
    return {paddingX, paddingY, width: rootLogicalWidth + 2 * paddingX, height: rootLogicalHeight + 2 * paddingY}
  }

  const reducedMotionQuery = typeof globalScope.matchMedia === "function"
    ? globalScope.matchMedia("(prefers-reduced-motion: reduce)")
    : null
  let effectiveFps = reducedMotionQuery?.matches
    ? Math.min(configuredFps, 12)
    : configuredFps

  if (documentScope?.head) {
    styleElement = documentScope.createElement("style")
    styleElement.id = STYLE_ID
    styleElement.textContent = [
      `.codex-avatar-root[${RENDERED_ATTRIBUTE}="${MARKER_TOKEN}"]{`,
      "background-image:none!important;",
      "}",
      `[data-codex-live2d-pointer-host="${MARKER_TOKEN}"]{pointer-events:none!important;}`,
      `[data-codex-live2d-hit-region="${MARKER_TOKEN}"]{pointer-events:auto!important;}`,
    ].join("")
    documentScope.head.appendChild(styleElement)
  }

  function errorMessage(reason) {
    if (reason && typeof reason.stack === "string" && reason.stack) {
      return reason.stack
    }
    if (reason instanceof Error) return reason.message || reason.name || "Unknown error"
    return String(reason || "Unknown error")
  }

  function normalizePath(value) {
    return String(value || "")
      .replace(/\\/g, "/")
      .replace(/^(\.\/)+/, "")
      .replace(/^\/+/, "")
  }

  function clearLastErrorForBoot() {
    if (
      lastError === "Codex pet root not found; waiting for the native DOM."
      || lastError === "Codex pet root is not available."
    ) {
      lastError = null
    }
  }

  function findPetRoot() {
    if (!documentScope) return null
    const roots = Array.from(documentScope.querySelectorAll(ROOT_SELECTOR))
    if (petId !== null) {
      const exact = roots.find((candidate) => candidate.getAttribute(PET_ID_ATTRIBUTE) === petId)
      if (exact) return exact
    }
    if (allowAnyPet) return roots[0] || null
    return null
  }

  function restoreMarker() {
    if (!markerRoot) return
    try {
      markerRoot.removeAttribute(RENDERED_ATTRIBUTE)
    } catch {
      // The old React root may already be detached.
    }
    markerRoot = null
  }

  function applyMarker() {
    if (!active || !root || !root.isConnected) {
      restoreMarker()
      return
    }
    if (markerRoot === root && root.getAttribute(RENDERED_ATTRIBUTE) === MARKER_TOKEN) return
    restoreMarker()
    root.removeAttribute(RENDERED_ATTRIBUTE)
    markerRoot = root
    root.setAttribute(RENDERED_ATTRIBUTE, MARKER_TOKEN)
  }

  function decodeBase64(value) {
    if (typeof globalScope.atob !== "function") {
      throw new Error("Browser atob() is unavailable")
    }
    const source = String(value || "")
      .replace(/^data:[^,]*;base64,/, "")
      .replace(/\s+/g, "")
    const binary = globalScope.atob(source)
    const bytes = new Uint8Array(binary.length)
    for (let index = 0; index < binary.length; index += 1) {
      bytes[index] = binary.charCodeAt(index)
    }
    return bytes
  }

  function inferMime(path) {
    const lower = path.toLowerCase()
    if (lower.endsWith(".json")) return "application/json"
    if (lower.endsWith(".png")) return "image/png"
    if (lower.endsWith(".webp")) return "image/webp"
    if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg"
    if (lower.endsWith(".moc3")) return "application/octet-stream"
    if (lower.endsWith(".wav")) return "audio/wav"
    if (lower.endsWith(".mp3")) return "audio/mpeg"
    if (lower.endsWith(".ogg")) return "audio/ogg"
    return "application/octet-stream"
  }

  function createBundleUrls(inputBundle) {
    if (!inputBundle || !inputBundle.model || !Array.isArray(inputBundle.files)) {
      throw new TypeError("config.bundle must contain model and files[]")
    }
    if (typeof globalScope.URL?.createObjectURL !== "function") {
      throw new Error("URL.createObjectURL() is unavailable")
    }

    const urls = []
    const textureUrls = []
    // Publish incrementally so a malformed model can still revoke every URL
    // created before parsing or reference validation fails.
    blobUrls = urls
    const fileUrls = new Map()
    const uniqueByBase = new Map()

    const remember = (path, url) => {
      if (fileUrls.has(path)) throw new Error(`Duplicate bundle path: ${path}`)
      fileUrls.set(path, url)
      const base = path.slice(path.lastIndexOf("/") + 1)
      const entries = uniqueByBase.get(base) || []
      entries.push({ path, url })
      uniqueByBase.set(base, entries)
    }

    for (const file of inputBundle.files) {
      const path = normalizePath(file?.path)
      if (!path) throw new TypeError("Every bundle file needs a non-empty path")
      const bytes = decodeBase64(file.base64)
      const mime = typeof file.mime === "string" && file.mime
        ? file.mime
        : inferMime(path)
      const url = globalScope.URL.createObjectURL(
        new Blob([bytes], { type: mime }),
      )
      urls.push(url)
      if (mime.startsWith("image/") || /\.(?:avif|jpe?g|png|webp)$/i.test(path)) {
        textureUrls.push(url)
      }
      remember(path, url)
    }

    if (fileUrls.size === 0) throw new Error("The Live2D bundle contains no files")

    function lookup(reference) {
      const normalized = normalizePath(reference)
      let decoded = normalized
      try {
        decoded = decodeURIComponent(normalized)
      } catch {
        // Keep the original spelling when it is not URI encoded.
      }

      const direct = fileUrls.get(normalized) || fileUrls.get(decoded)
      if (direct) return direct

      const suffixMatches = []
      for (const [path, url] of fileUrls) {
        if (path.endsWith(`/${normalized}`) || path.endsWith(`/${decoded}`)) {
          suffixMatches.push({ path, url })
        }
      }
      if (suffixMatches.length === 1) return suffixMatches[0].url

      const base = decoded.slice(decoded.lastIndexOf("/") + 1)
      const baseMatches = uniqueByBase.get(base) || []
      if (baseMatches.length === 1) return baseMatches[0].url

      throw new Error(`Bundle file not found for model reference: ${reference}`)
    }

    function rewriteReference(value) {
      if (typeof value !== "string" || value.length === 0) {
        throw new TypeError("A model3 file reference must be a non-empty string")
      }
      return lookup(value)
    }

    const rewritten = JSON.parse(JSON.stringify(inputBundle.model))
    const fileReferences = rewritten.FileReferences
    if (!fileReferences || typeof fileReferences !== "object") {
      throw new Error("model3 has no FileReferences object")
    }

    for (const key of ["Moc", "Physics", "Pose", "DisplayInfo", "UserData"]) {
      if (fileReferences[key] !== undefined) {
        fileReferences[key] = rewriteReference(fileReferences[key])
      }
    }
    if (!Array.isArray(fileReferences.Textures)) {
      throw new Error("model3 FileReferences.Textures must be an array")
    }
    fileReferences.Textures = fileReferences.Textures.map(rewriteReference)

    if (fileReferences.Expressions !== undefined) {
      if (!Array.isArray(fileReferences.Expressions)) {
        throw new Error("model3 FileReferences.Expressions must be an array")
      }
      for (const expression of fileReferences.Expressions) {
        if (expression?.File !== undefined) {
          expression.File = rewriteReference(expression.File)
        }
      }
    }

    if (fileReferences.Motions !== undefined) {
      const motionGroups = fileReferences.Motions
      if (!motionGroups || typeof motionGroups !== "object") {
        throw new Error("model3 FileReferences.Motions must be an object")
      }
      for (const motions of Object.values(motionGroups)) {
        if (!Array.isArray(motions)) continue
        for (const motion of motions) {
          if (motion?.File !== undefined) {
            motion.File = rewriteReference(motion.File)
          }
          if (motion?.Sound !== undefined) {
            motion.Sound = rewriteReference(motion.Sound)
          }
        }
      }
    }

    const modelUrl = globalScope.URL.createObjectURL(
      new Blob([JSON.stringify(rewritten)], { type: "application/json" }),
    )
    urls.push(modelUrl)
    return { modelUrl, urls, textureUrls }
  }

  function setCanvasStyle(targetCanvas) {
    const bounds = canvasMetrics()
    targetCanvas.setAttribute("aria-hidden", "true")
    targetCanvas.setAttribute("data-codex-live2d-canvas", "true")
    Object.assign(targetCanvas.style, {
      position: "absolute",
      inset: "auto",
      left: `${-bounds.paddingX}px`,
      top: `${-bounds.paddingY}px`,
      width: `${bounds.width}px`,
      height: `${bounds.height}px`,
      maxWidth: "none",
      maxHeight: "none",
      display: "block",
      pointerEvents: "none",
      userSelect: "none",
      zIndex: "2",
      opacity: active ? "1" : "0",
    })
  }

  function mountCanvas(targetRoot) {
    if (!canvas) return
    if (!targetRoot || !targetRoot.isConnected) {
      if (canvas.parentNode) canvas.remove()
      active = false
      restoreMarker()
      updatePlayback()
      return
    }
    if (canvas.parentElement !== targetRoot) targetRoot.appendChild(canvas)
    setCanvasStyle(canvas)
    activateIfMounted()
  }

  function layout() {
    if (disposed) return
    if (root?.isConnected) {
      const rect = root.getBoundingClientRect()
      rootLogicalWidth = Math.max(1, Math.round(rect.width || root.clientWidth || 1))
      rootLogicalHeight = Math.max(1, Math.round(rect.height || root.clientHeight || 1))
    } else {
      rootLogicalWidth = 1
      rootLogicalHeight = 1
    }

    if (!app || !model || !sourceSize) return
    const renderer = app.renderer
    const bounds = canvasMetrics()
    const resolution = Math.min(
      2,
      Math.max(1, Number(globalScope.devicePixelRatio) || 1),
    )
    try {
      if (typeof renderer.resize === "function") {
        renderer.resize(bounds.width, bounds.height, resolution)
      }
    } catch {
      if (typeof renderer.resize === "function") {
        renderer.resize(bounds.width, bounds.height)
      }
    }

    model.anchor?.set?.(0.5, 0.5)
    applyModelFit()
    model.position?.set?.(bounds.paddingX + rootLogicalWidth / 2, bounds.paddingY + rootLogicalHeight / 2)
    if (canvas) setCanvasStyle(canvas)
    installHitRegions()
  }

  function clearHitRegions() {
    hitLayer?.remove()
    hitLayer = null
    for (const host of hitHosts) host.removeAttribute("data-codex-live2d-pointer-host")
    hitHosts = []
  }

  function buildHitMask() {
    try {
      const image = app.renderer.extract.canvas({target: app.stage, frame: app.renderer.screen.clone(), resolution: 1, clearColor: [0, 0, 0, 0]})
      const pixels = image.getContext("2d").getImageData(0, 0, image.width, image.height).data
      const bounds = canvasMetrics()
      const rows = []
      const stride = Math.max(2, Math.round(rootLogicalHeight / 28))
      for (let y = 0; y < rootLogicalHeight; y += stride) {
        let left = rootLogicalWidth, right = -1
        for (let yy = y; yy < Math.min(rootLogicalHeight, y + stride); yy++) {
          for (let x = 0; x < rootLogicalWidth; x++) {
            const at = ((yy + bounds.paddingY) * image.width + x + bounds.paddingX) * 4 + 3
            if (pixels[at] >= 40) { left = Math.min(left, x); right = Math.max(right, x) }
          }
        }
        if (right < left) continue
        const x1 = Math.max(0, left - 1), x2 = Math.min(rootLogicalWidth, right + 2)
        rows.push({x: x1 / rootLogicalWidth, y: y / rootLogicalHeight, width: (x2 - x1) / rootLogicalWidth, height: Math.min(stride, rootLogicalHeight - y) / rootLogicalHeight})
      }
      if (!rows.length) throw new Error("No visible character pixels found")
      hitMask = rows
      hitAreaError = null
      installHitRegions()
    } catch (reason) {
      hitAreaError = errorMessage(reason)
    }
  }

  function installHitRegions() {
    if (!root?.isConnected || !hitMask.length) return
    if (hitLayer?.parentElement === root && hitHosts.every(host => host.contains(root))) return
    clearHitRegions()
    const host = root.closest('[data-avatar-overlay-hit-region="mascot"]') || root.closest('[data-avatar-mascot]') || root
    const rect = root.getBoundingClientRect()
    let reachedHost = false
    for (let element = root; element && element !== documentScope.body; element = element.parentElement) {
      const box = element.getBoundingClientRect()
      if (reachedHost && (Math.abs(box.width - rect.width) > 2 || Math.abs(box.height - rect.height) > 2)) break
      if ([...element.querySelectorAll('[data-avatar-overlay-hit-region]')].some(region => !region.contains(root))) break
      element.setAttribute("data-codex-live2d-pointer-host", MARKER_TOKEN)
      hitHosts.push(element)
      if (element === host) reachedHost = true
    }
    hitLayer = documentScope.createElement("div")
    hitLayer.setAttribute("data-codex-live2d-hit-layer", "true")
    Object.assign(hitLayer.style, {position: "absolute", inset: "0", pointerEvents: "none", zIndex: "3"})
    for (const area of hitMask) {
      const region = documentScope.createElement("div")
      region.setAttribute("data-codex-live2d-hit-region", MARKER_TOKEN)
      region.setAttribute("data-avatar-overlay-hit-region", "mascot-artwork")
      Object.assign(region.style, {position: "absolute", left: `${area.x * 100}%`, top: `${area.y * 100}%`, width: `${area.width * 100}%`, height: `${area.height * 100}%`, pointerEvents: "auto"})
      hitLayer.appendChild(region)
    }
    root.appendChild(hitLayer)
  }

  function applyModelFit() {
    if (!model || !sourceSize) return
    const fit = Math.min(
      rootLogicalWidth / sourceSize.width,
      rootLogicalHeight / sourceSize.height,
    ) * 0.88 * 1.06
    model.scale?.set?.(fit)
  }

  // Codex also expands controls within a 40px proximity radius, independently
  // of the DOM input regions. Gate that one incoming action with artwork hover.
  function createControlsHover() {
    if (!root?.closest('[data-avatar-overlay-hit-region="mascot"]') || !hitMask.length) return null
    let near = null, nativeNear = null, point = null, hideTimer = null, dragging = false, dead = false
    const ownEvents = new WeakSet()
    const inside = (p, r) => p && p.x >= r.left && p.x <= r.right && p.y >= r.top && p.y <= r.bottom
    function publish(value, force = false) {
      if (dead || !force && near === value) return
      near = value
      const event = new MessageEvent("message", {data: {type: "avatar-overlay-action", action: {type: "pet-pointer-proximity-changed", isNear: value}}})
      ownEvents.add(event)
      globalScope.dispatchEvent(event)
    }
    function onArtwork(p) {
      if (!p || !root?.isConnected) return false
      const r = root.getBoundingClientRect()
      const x = (p.x - r.left) / r.width, y = (p.y - r.top) / r.height
      return hitMask.some(a => x >= a.x && x <= a.x + a.width && y >= a.y && y <= a.y + a.height)
    }
    function onControl(p) {
      if (!p) return false
      for (const element of documentScope.querySelectorAll('[data-avatar-overlay-native-surface-id], [data-avatar-overlay-hit-region]')) {
        if (element.contains(root) || root?.contains(element)) continue
        const r = element.getBoundingClientRect(), style = globalScope.getComputedStyle(element)
        if (r.width < 10 || r.height < 10 || !inside(p, r) || style.pointerEvents === "none" || style.visibility !== "visible" || style.display === "none") continue
        let opacity = 1
        for (let e = element; e; e = e.parentElement) opacity *= Number(globalScope.getComputedStyle(e).opacity || 1)
        if (opacity > 0.1) return true
      }
      return false
    }
    function update() {
      if (dead) return
      if (disposed || !active || !root?.isConnected) {
        if (hideTimer !== null) { globalScope.clearTimeout(hideTimer); hideTimer = null }
        near = null
        return
      }
      if (!disposed && active && documentScope.visibilityState !== "hidden" && (dragging || onArtwork(point) || onControl(point))) {
        if (hideTimer !== null) { globalScope.clearTimeout(hideTimer); hideTimer = null }
        publish(true)
      } else if (near === true) {
        if (hideTimer === null) hideTimer = globalScope.setTimeout(() => { hideTimer = null; publish(false) }, 180)
      } else publish(false)
    }
    function move(event) { point = {x: event.clientX, y: event.clientY}; update() }
    function leave() { point = null; update() }
    function down(event) { if (event.button === 0 && onArtwork({x: event.clientX, y: event.clientY})) dragging = true }
    function up() { dragging = false; update() }
    function incoming(event) {
      if (ownEvents.has(event) || event.data?.type !== "avatar-overlay-action" || event.data.action?.type !== "pet-pointer-proximity-changed") return
      nativeNear = event.data.action.isNear === true
      if (!nativeNear) point = null
      if (disposed || !active || !root?.isConnected) { update(); return }
      event.stopImmediatePropagation()
      update()
      // Codex may have a capture listener registered before this adapter.
      // Re-publish our result in the same dispatch so React receives the final
      // artwork-based value even when it already saw the native radius event.
      publish(near === true, true)
    }
    globalScope.addEventListener("message", incoming, true)
    globalScope.addEventListener("mousemove", move, {capture: true, passive: true})
    globalScope.addEventListener("pointermove", move, {capture: true, passive: true})
    globalScope.addEventListener("mouseleave", leave, true)
    globalScope.addEventListener("pointerdown", down, true)
    globalScope.addEventListener("pointerup", up, true)
    globalScope.addEventListener("pointercancel", up, true)
    publish(false, true)
    return {
      status() { return {enabled: true, expanded: near === true, nativeNear, point, hideDelayMs: 180} },
      dispose() {
        if (dead) return
        globalScope.removeEventListener("message", incoming, true)
        globalScope.removeEventListener("mousemove", move, true)
        globalScope.removeEventListener("pointermove", move, true)
        globalScope.removeEventListener("mouseleave", leave, true)
        globalScope.removeEventListener("pointerdown", down, true)
        globalScope.removeEventListener("pointerup", up, true)
        globalScope.removeEventListener("pointercancel", up, true)
        if (hideTimer !== null) globalScope.clearTimeout(hideTimer)
        if (nativeNear !== null) publish(nativeNear, true)
        dead = true
      },
    }
  }

  function renderOnce() {
    if (!app) throw new Error("Pixi Application is not ready")
    if (typeof app.render === "function") {
      app.render()
    } else {
      app.renderer.render(app.stage)
    }
    frames += 1
  }

  function expressionManager() {
    return model?.internalModel?.motionManager?.expressionManager || null
  }

  function availableExpressionName(candidate) {
    const definitions = expressionManager()?.definitions
    if (!Array.isArray(definitions) || definitions.length === 0) return null
    const wanted = String(candidate).toLocaleLowerCase()
    const definition = definitions.find((item) => (
      item
      && typeof item.File === "string"
      && typeof item.Name === "string"
      && item.Name.toLocaleLowerCase() === wanted
    ))
    return definition?.Name || null
  }

  function wantedExpression() {
    if (appearance.expression === "none") return null
    if (appearance.expression !== "auto") return availableExpressionName(appearanceCatalog.expressions.find(item => item.id === appearance.expression)?.name)
    if (state === "idle" && motionExpression) {
      const available = availableExpressionName(motionExpression)
      if (available) return available
    }
    if (state === "idle" && idleExpression) return availableExpressionName(idleExpression)
    const candidates = STATE_EXPRESSIONS[state] || []
    for (const candidate of candidates) {
      const available = availableExpressionName(candidate)
      if (available) return available
    }
    return null
  }

  async function applyStateExpression() {
    if (
      disposed
      || !active
      || !modelReady
      || !model
    ) return
    const manager = expressionManager()
    if (!manager || !Array.isArray(manager.definitions) || manager.definitions.length === 0) {
      return
    }

    const wanted = wantedExpression()
    if (wanted === null) {
      if (currentExpressionName !== null) {
        manager.resetExpression?.()
        currentExpressionName = null
      }
      return
    }
    if (wanted === currentExpressionName) return

    const requestedModel = model
    const existingIndex = typeof manager.getExpressionIndex === "function"
      ? manager.getExpressionIndex(wanted)
      : manager.definitions.findIndex((definition) => definition.Name === wanted)
    // The engine's resetExpression() applies the default pose but deliberately
    // keeps currentExpression. Re-selecting that same expression then returns
    // false. restoreExpression() is the matching API for this transition.
    if (
      existingIndex >= 0
      && manager.expressions?.[existingIndex]
      && manager.currentExpression === manager.expressions[existingIndex]
      && typeof manager.restoreExpression === "function"
    ) {
      manager.restoreExpression()
      currentExpressionName = wanted
      lastWarning = null
      return
    }
    const changed = await requestedModel.expression(wanted)
    if (disposed || model !== requestedModel || wantedExpression() !== wanted) return
    if (changed) {
      currentExpressionName = wanted
      lastWarning = null
    } else if (
      existingIndex >= 0
      && manager.expressions?.[existingIndex]
      && manager.currentExpression === manager.expressions[existingIndex]
    ) {
      manager.restoreExpression?.()
      currentExpressionName = wanted
      lastWarning = null
    } else {
      lastWarning = `Expression could not be applied: ${wanted}`
    }
  }

  function scheduleStateExpression() {
    if (disposed) return
    expressionQueue = expressionQueue
      .then(applyStateExpression)
      .catch((reason) => {
        if (!disposed) lastWarning = errorMessage(reason)
      })
  }

  function setStateInternal(nextState) {
    const previousState = state
    state = typeof nextState === "string" && nextState ? nextState : "idle"
    if (state !== previousState) {
      motionExpression = null
      idleExpression = null
      motionDirector?.stateChanged(state)
    }
    scheduleStateExpression()
  }

  // The model's action files all declare Loop=true. Run finite, interruptible
  // reactions and restore action-only parameters on the motion update seam.
  // Lifecycle details informed by the MIT-licensed upstream motion controller.
  function createMotionDirector(loaded, vendor) {
    const intrinsic = loaded.internalModel
    const manager = intrinsic.motionManager
    const raw = intrinsic.coreModel?._model?.parameters
    const catalog = bundle.motionCatalog || {}
    const index = new Map(Array.from(raw?.ids || []).map((id, at) => [id, at]))
    const initial = new Map(Array.from(index, ([id, at]) => [id, raw.values[at]]))
    const idleIds = new Set(catalog.Idle?.parameters || [])
    const natural = /^Param(Angle|Body|Breath|Hair|Eye|Mouth|Brow)/
    const actionIds = new Set(Object.entries(catalog)
      .filter(([group]) => group !== "Idle")
      .flatMap(([, entry]) => entry.parameters || [])
      .filter(id => !idleIds.has(id) && !natural.test(id)))
    actionIds.add("jingyu")
    const labels = { Hammer: "敲桌", BubbleGum: "吹泡泡", SprayWater: "鲸鱼喷水", OpenCase: "拿手机", Selfie: "自拍", SelfieQuick: "快速自拍", Ketchup: "挤番茄酱" }
    const faces = { BubbleGum: "调皮", OpenCase: "脸红", Selfie: "开心兴奋", SelfieQuick: "星星眼", Ketchup: "情绪花花" }
    const reactions = { running: "Hammer", waiting: "BubbleGum", review: "Selfie", failed: "SprayWater", waving: "SelfieQuick", jumping: "SelfieQuick" }
    const pools = { idle: ["BubbleGum", "OpenCase", "Selfie", "SelfieQuick", "Ketchup"], running: ["Hammer", "OpenCase", "SelfieQuick"], waiting: ["BubbleGum", "OpenCase"] }
    const bags = new Map()
    const reactionAt = new Map()
    const history = []
    let clock = 0, nextAt = 2000, lastPointerAt = -5000
    let current = null, pending = null, sequence = 0, dead = false
    let lastGroup = null, lastEngineGroup = null, completed = 0
    let lastReactionAt = -Infinity
    let nextBlink = 2400 + Math.random() * 1600, blinkStart = null, blinkAmount = 0, blinkCount = 0
    let nextFaceAt = 8000, faceUntil = 0, lastFace = null
    const restored = new Map()
    const resting = () => 4500 + Math.random() * 4000
    const write = (id, value) => { const at = index.get(id); if (at !== undefined) raw.values[at] = value }
    const available = group => Boolean(catalog[group] && manager.definitions?.[group]?.length)
    const isPlaying = () => !dead && !disposed && loaded === model && active && playbackRunning

    function parametersAfterMotion() {
      if (dead || !raw) return
      for (const id of actionIds) {
        if (current?.owned.has(id)) continue
        const target = initial.get(id)
        if (target === undefined) continue
        const returning = restored.get(id)
        const t = returning ? Math.min(1, (clock - returning.at) / 240) : 1
        write(id, returning ? returning.value + (target - returning.value) * t : target)
        if (t === 1) restored.delete(id)
      }
      if (current?.group === "SprayWater") {
        write("jingyu", 1)
        if (clock >= current.nominalEndAt) write("pengshui", 1)
      }
      if (current?.step > 0 && ["Selfie", "SelfieQuick"].includes(current.group)) {
        write("phone", 1)
        write("phone2", 0)
      }
    }

    function blinkBeforeDrawing() {
      if (dead || !raw || blinkAmount <= 0) return
      for (const id of ["ParamEyeLOpen", "ParamEyeROpen"]) {
        const at = index.get(id)
        if (at !== undefined) raw.values[at] *= 1 - blinkAmount
      }
    }

    function resetProps() {
      if (current && raw) {
        for (const id of current.owned) {
          const at = index.get(id)
          if (at !== undefined && actionIds.has(id)) restored.set(id, {value: raw.values[at], at: clock})
        }
      }
      current = null
      motionExpression = null
      scheduleStateExpression()
    }

    function returnIdle() {
      if (dead || loaded !== model) return
      resetProps()
      nextAt = clock + resting()
      lastEngineGroup = "Idle"
      Promise.resolve(loaded.motion("Idle", 0, vendor.MotionPriority.FORCE, {loop: true, resetExpression: false}))
        .catch(reason => { if (!dead) lastWarning = `Idle motion: ${errorMessage(reason)}` })
    }

    async function startStep(ticket) {
      const action = current
      if (!action || action.ticket !== ticket || dead) return false
      const group = action.steps[action.step]
      action.loading = true
      try {
        if (lastEngineGroup === group) manager.stopAllMotions()
        lastEngineGroup = group
        const started = await loaded.motion(group, 0, vendor.MotionPriority.FORCE, {loop: false, resetExpression: false})
        if (dead || current?.ticket !== ticket) return false
        if (!started) throw new Error(`Motion start was declined: ${group}`)
        action.loading = false
        action.nominalEndAt = clock + catalog[group].durationMs
        action.deadline = clock + catalog[group].durationMs
          + (action.step < action.steps.length - 1 ? 160 : group === "OpenCase" ? 900 : group === "SprayWater" ? 1100 : 120)
        return true
      } catch (reason) {
        if (!dead && current?.ticket === ticket) {
          lastWarning = `Motion ${group}: ${errorMessage(reason)}`
          returnIdle()
        }
        return false
      }
    }

    function play(group, source = "manual") {
      if (!isPlaying() || !available(group) || group === "Idle") return false
      resetProps()
      idleExpression = null
      const steps = ["Selfie", "SelfieQuick"].includes(group) && available("OpenCase") ? ["OpenCase", group] : [group]
      const owned = new Set(steps.flatMap(step => catalog[step].parameters))
      if (group === "SprayWater") owned.add("jingyu")
      for (const id of owned) restored.delete(id)
      current = {group, source, ticket: ++sequence, steps, step: 0, owned, loading: true, startedAt: clock, deadline: Infinity}
      lastGroup = group
      history.push({group, label: labels[group] || group, source, atMs: Math.round(clock)})
      if (source === "native-state") { reactionAt.set(group, clock); lastReactionAt = clock }
      if (history.length > 12) history.shift()
      if (state === "idle") motionExpression = faces[group] || null
      scheduleStateExpression()
      void startStep(current.ticket)
      return true
    }

    function pick() {
      const poolName = pools[state] ? state : state.startsWith("running") ? "running" : "idle"
      let bag = bags.get(poolName)
      if (!bag?.length) {
        bag = (pools[poolName] || pools.idle).filter(available)
        for (let i = bag.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [bag[i], bag[j]] = [bag[j], bag[i]] }
        bags.set(poolName, bag)
      }
      if (bag.length > 1 && bag.at(-1) === lastGroup) [bag[0], bag[bag.length - 1]] = [bag.at(-1), bag[0]]
      return bag.pop()
    }

    function stateChanged(nextState) {
      const group = reactions[nextState]
      const fresh = group && clock - (reactionAt.get(group) ?? -Infinity) >= (nextState === "failed" ? 5000 : 12000)
        && (nextState === "failed" || clock - lastReactionAt >= 6000)
      pending = fresh && available(group) ? {group, state: nextState} : null
      if (pending && nextState === "failed" && current) { ++sequence; resetProps() }
      nextAt = Math.min(nextAt, clock + 3500)
    }

    function tick(delta) {
      if (!isPlaying()) return
      clock += Math.max(0, Math.min(100, Number(delta) || 16.67))
      if (state !== "idle" || current || appearance.expression !== "auto" || !appearance.autoExpressions) {
        if (idleExpression) { idleExpression = null; scheduleStateExpression() }
      } else if (idleExpression && clock >= faceUntil) {
        idleExpression = null; scheduleStateExpression()
      } else if (!idleExpression && clock >= nextFaceAt) {
        const choices = appearanceCatalog.automaticExpressions.filter(name => name !== lastFace && availableExpressionName(name))
        idleExpression = choices[Math.floor(Math.random() * choices.length)] || null
        lastFace = idleExpression; faceUntil = clock + 3200; nextFaceAt = clock + 12000 + Math.random() * 8000
        scheduleStateExpression()
      }
      if (blinkStart !== null) {
        const t = (clock - blinkStart) / 240
        blinkAmount = t < 1 ? Math.sin(Math.PI * t) ** 2 : 0
        if (t >= 1) { blinkStart = null; nextBlink = clock + 2600 + Math.random() * 3000; blinkCount++ }
      } else if (clock >= nextBlink) blinkStart = clock
      if (clock - lastPointerAt > 4000 && !current) setGaze(Math.sin(clock / 2800) * 0.23, Math.cos(clock / 3900) * 0.12)
      if (current && !current.loading && clock >= current.deadline) {
        if (current.step < current.steps.length - 1) { current.step++; void startStep(current.ticket) }
        else { completed++; returnIdle() }
      }
      if (current) return
      if (pending) {
        const next = pending; pending = null
        if (next.state === state) { play(next.group, "native-state"); return }
      }
      if (supplied.autoMotions !== false && clock >= nextAt) {
        const group = pick()
        if (group) play(group, "ambient")
        else nextAt = clock + resting()
      }
    }

    intrinsic.on("afterMotionUpdate", parametersAfterMotion)
    intrinsic.on("beforeModelUpdate", blinkBeforeDrawing)
    stateChanged(state)
    return {
      tick, play, stateChanged,
      pointer() { lastPointerAt = clock },
      status() {
        return {group: current?.group || "Idle", label: current ? labels[current.group] : "待机", source: current?.source || null, step: current?.steps[current.step] || null, loading: current?.loading || false, completed, blinkCount, clockMs: Math.round(clock), nextInMs: Math.max(0, Math.round(nextAt - clock)), history: history.slice(), parameters: Object.fromEntries(["phone", "phone2", "phone5", "jingyu", "pengshui", "chuipaopao", "danbaofan"].filter(id => index.has(id)).map(id => [id, raw.values[index.get(id)]]))}
      },
      dispose() {
        dead = true; sequence++; current = null; pending = null
        intrinsic.off("afterMotionUpdate", parametersAfterMotion)
        intrinsic.off("beforeModelUpdate", blinkBeforeDrawing)
      },
    }
  }

  function createAppearanceLayer(loaded) {
    const intrinsic = loaded.internalModel, raw = intrinsic.coreModel?._model?.parameters
    const indices = new Map(Array.from(raw?.ids || []).map((id, index) => [id, index]))
    let applied = {}
    function apply() {
      applied = {}
      if (!raw) return
      for (const group of appearanceCatalog.groups) {
        // Let phone/hammer gestures finish before applying a static hand pose.
        if (group.id === "hands" && motionDirector?.status().group !== "Idle") continue
        const option = group.options.find(option => option.id === appearance.accessories[group.id])
        for (const parameter of option?.parameters || []) {
          const at = indices.get(parameter.Id)
          if (at === undefined) continue
          const value = Number(parameter.Value), base = raw.values[at]
          const next = parameter.Blend === "Multiply" ? base * value : parameter.Blend === "Overwrite" ? value : base + value
          raw.values[at] = Math.max(raw.minimumValues?.[at] ?? -Infinity, Math.min(raw.maximumValues?.[at] ?? Infinity, next))
          applied[parameter.Id] = raw.values[at]
        }
      }
      if (desktopEffect && globalScope.performance.now() >= desktopEffect.until) desktopEffect = null
      for (const parameter of desktopEffect?.parameters || []) {
        const at = indices.get(parameter.Id)
        if (at === undefined) continue
        const next = parameter.Blend === "Multiply" ? raw.values[at] * parameter.Value : parameter.Blend === "Overwrite" ? parameter.Value : raw.values[at] + parameter.Value
        raw.values[at] = Math.max(raw.minimumValues?.[at] ?? -Infinity, Math.min(raw.maximumValues?.[at] ?? Infinity, next))
        applied[parameter.Id] = raw.values[at]
      }
    }
    // Cubism restores saved motion parameters after model.update(). Applying
    // here composes accessories over the facial expression without accumulating.
    intrinsic.on("beforeModelUpdate", apply)
    return {status: () => ({...applied}), dispose() { intrinsic.off("beforeModelUpdate", apply); applied = {} }}
  }

  function setAppearance(value) {
    const next = normalizeAppearance(value)
    if (JSON.stringify(next) !== JSON.stringify(appearance)) {
      appearance = next; idleExpression = null
      scheduleStateExpression()
      appearanceHitRefresh = frames + 24
      if (supplied.preview && !previewPlaying) { previewFrames = 30; updatePlayback() }
    }
    return status()
  }

  function playEffect(id) {
    const effect = appearanceCatalog.effects?.find(effect => effect.id === id)
    if (!effect || disposed || !active) return false
    if (supplied.preview && !previewPlaying) previewFrames = Math.ceil((effect.durationMs + 500) * effectiveFps / 1000)
    updatePlayback()
    if (!playbackRunning) return false
    if (effect.motion && !motionDirector?.play(effect.motion)) return false
    desktopEffect = {...effect, until: globalScope.performance.now() + effect.durationMs}
    return true
  }

  function setPreviewPlaying(value) {
    if (!supplied.preview) return false
    previewPlaying = Boolean(value); previewFrames = 0; updatePlayback(); return true
  }

  function setFps(value) {
    if (![30, 60].includes(value)) throw new RangeError("Choose 30 or 60 FPS")
    configuredFps = value; effectiveFps = reducedMotionQuery?.matches ? Math.min(value, 12) : value
    updatePlayback(); return status()
  }

  function setGaze(x, y) {
    if (!modelReady || !model || disposed) return
    try {
      model.internalModel?.focusController?.focus?.(x, y)
    } catch {
      // Models without the standard focus controller remain usable.
    }
  }

  function onPointerMove(event) {
    if (
      disposed
      || !active
      || !root?.isConnected
      || reducedMotionQuery?.matches
      || documentScope?.visibilityState === "hidden"
    ) return
    motionDirector?.pointer()
    const rect = root.getBoundingClientRect()
    if (rect.width <= 0 || rect.height <= 0) return
    const centerX = rect.left + rect.width / 2
    const centerY = rect.top + rect.height / 2
    const range = Math.max(
      120,
      Math.min(
        Number(globalScope.innerWidth) || 1200,
        Number(globalScope.innerHeight) || 800,
      ) * 0.38,
    )
    const deadZone = 0.025
    const shape = (value) => {
      const size = Math.abs(value)
      if (size <= deadZone) return 0
      const ramped = Math.min(1, (size - deadZone) / (1 - deadZone))
      return value < 0 ? -ramped : ramped
    }
    let x = shape((event.clientX - centerX) / range)
    let y = shape((event.clientY - centerY) / range)
    const length = Math.hypot(x, y)
    if (length > 1) {
      x /= length
      y /= length
    }
    setGaze(x, -y)
  }

  function onPointerOut(event) {
    if (event.relatedTarget === null) setGaze(0, 0)
  }

  function onVisibilityChange() {
    if (documentScope?.visibilityState === "hidden") {
      setGaze(0, 0)
    }
    updatePlayback()
  }

  function updatePlayback() {
    const shouldRun = Boolean(
      !disposed
      && active
      && rootVisible
      && root?.isConnected
      && canvas?.isConnected
      && documentScope?.visibilityState !== "hidden"
      && !reducedMotionQuery?.matches
      && (!supplied.preview || previewPlaying || previewFrames > 0)
      && app?.ticker
    )
    if (!app?.ticker) {
      playbackRunning = false
      return
    }
    try {
      app.ticker.maxFPS = effectiveFps
      if (shouldRun) {
        app.ticker.start()
      } else {
        app.ticker.stop()
      }
    } catch {
      playbackRunning = false
      return
    }
    playbackRunning = shouldRun
  }

  function activateIfMounted() {
    const canActivate = Boolean(
      !disposed
      && modelReady
      && renderedOnce
      && root?.isConnected
      && canvas?.isConnected
      && canvas.parentElement === root
    )
    active = canActivate
    if (canvas) canvas.style.opacity = active ? "1" : "0"
    if (active) {
      applyMarker()
      scheduleStateExpression()
    } else {
      restoreMarker()
    }
    updatePlayback()
  }

  function observeRoot(targetRoot) {
    resizeObserver?.disconnect()
    intersectionObserver?.disconnect()
    rootObserver?.disconnect()
    rootObserver = null
    rootVisible = true

    if (!targetRoot) return

    if (typeof globalScope.MutationObserver === "function") {
      rootObserver = new globalScope.MutationObserver((records) => {
        if (disposed) return
        for (const record of records) {
          if (record.type === "attributes" && record.attributeName === PET_ID_ATTRIBUTE) {
            scheduleReconcile()
          }
          if (record.type === "attributes" && record.attributeName === STATE_ATTRIBUTE) {
            const nativeState = root?.getAttribute(STATE_ATTRIBUTE) || "idle"
            if (nativeState !== lastNativeState) {
              lastNativeState = nativeState
              forcedState = null
              setStateInternal(nativeState)
            }
          }
        }
      })
      rootObserver.observe(targetRoot, {
        attributes: true,
        attributeFilter: [PET_ID_ATTRIBUTE, STATE_ATTRIBUTE],
      })
    }

    if (typeof globalScope.ResizeObserver === "function") {
      resizeObserver = new globalScope.ResizeObserver(() => layout())
      resizeObserver.observe(targetRoot)
    }

    if (typeof globalScope.IntersectionObserver === "function") {
      intersectionObserver = new globalScope.IntersectionObserver((entries) => {
        const entry = entries[entries.length - 1]
        rootVisible = Boolean(entry?.isIntersecting && (entry.intersectionRatio > 0 || targetRoot.offsetWidth > 0))
        updatePlayback()
      })
      intersectionObserver.observe(targetRoot)
    }
  }

  function attachRoot(nextRoot) {
    if (nextRoot === root && nextRoot) {
      mountCanvas(nextRoot)
      layout()
      return
    }

    clearHitRegions()
    restoreMarker()
    resizeObserver?.disconnect()
    intersectionObserver?.disconnect()
    rootObserver?.disconnect()
    resizeObserver = null
    intersectionObserver = null
    rootObserver = null
    root = nextRoot

    if (root?.isConnected) {
      // A cloneNode-based React replacement can copy our old rendered marker.
      // Treat it as stale until this instance has painted successfully.
      root.removeAttribute(RENDERED_ATTRIBUTE)
      lastNativeState = root.getAttribute(STATE_ATTRIBUTE) || "idle"
      observeRoot(root)
      if (forcedState === null) {
        setStateInternal(lastNativeState)
      } else {
        setStateInternal(forcedState)
      }
      mountCanvas(root)
      layout()
    } else {
      active = false
      if (canvas?.parentNode) canvas.remove()
      updatePlayback()
    }
  }

  function scheduleReconcile() {
    if (reconcileQueued || disposed) return
    reconcileQueued = true
    const schedule = typeof globalScope.queueMicrotask === "function"
      ? globalScope.queueMicrotask.bind(globalScope)
      : (callback) => Promise.resolve().then(callback)
    schedule(() => {
      reconcileQueued = false
      if (disposed || !documentScope) return
      const nextRoot = findPetRoot()
      if (nextRoot !== root) attachRoot(nextRoot)
      else if (nextRoot) mountCanvas(nextRoot)
      if (nextRoot) void ensureBoot()
    })
  }

  function startDocumentObserver() {
    if (
      documentObserver
      || typeof globalScope.MutationObserver !== "function"
      || !documentScope?.documentElement
    ) return
    documentObserver = new globalScope.MutationObserver((records) => {
      if (disposed) return
      for (const record of records) {
        if (record.type !== "childList") continue
        for (const node of [...record.addedNodes, ...record.removedNodes]) {
          if (node.nodeType !== 1) continue
          if (
            node.matches?.(ROOT_SELECTOR)
            || node.querySelector?.(ROOT_SELECTOR)
          ) {
            scheduleReconcile()
            return
          }
        }
      }
    })
    documentObserver.observe(documentScope.documentElement, {
      childList: true,
      subtree: true,
    })
  }

  function destroyEngine() {
    appearanceLayer?.dispose()
    appearanceLayer = null
    idleExpression = null
    controlsHover?.dispose()
    controlsHover = null
    clearHitRegions()
    hitMask = []
    motionDirector?.dispose()
    motionDirector = null
    motionExpression = null
    active = false
    modelReady = false
    renderedOnce = false
    playbackRunning = false
    restoreMarker()
    currentExpressionName = null

    if (canvas) {
      canvas.style.opacity = "0"
      if (contextLostHandler) {
        canvas.removeEventListener("webglcontextlost", contextLostHandler)
      }
      if (canvas.parentNode) canvas.remove()
    }

    const oldApp = app
    const oldModel = model
    app = null
    model = null
    canvas = null

    if (oldApp) {
      try {
        oldApp.destroy(
          { removeView: true },
          { children: true, texture: true, textureSource: true },
        )
      } catch {
        try {
          oldModel?.destroy({ children: true, texture: true })
        } catch {
          // Partial initialization may already have released the model.
        }
      }
    } else if (oldModel) {
      try {
        oldModel.destroy({ children: true, texture: true })
      } catch {
        // Partial initialization may already have released the model.
      }
    }

    for (const url of blobUrls) {
      try {
        globalScope.URL.revokeObjectURL(url)
      } catch {
        // Ignore an already-revoked URL.
      }
    }
    blobUrls = []
  }

  function fail(reason) {
    if (disposed) return
    lastError = `[${bootStage}] ${errorMessage(reason)}`
    destroyEngine()
    // A fatal boot/render failure must restore the native background even if
    // this or an older instance left a matching marker in the DOM.
    styleElement?.remove()
    styleElement = null
  }

  async function boot() {
    if (disposed) return
    clearLastErrorForBoot()
    bootStage = "core and vendor setup"
    if (!documentScope) throw new Error("This adapter must run in a browser document")
    const core = globalScope.Live2DCubismCore
    const vendor = globalScope.__dshLive2dPetVendor
    if (!core) throw new Error("Live2DCubismCore is not loaded")
    if (!vendor) throw new Error("__dshLive2dPetVendor is not loaded")
    for (const key of [
      "Application",
      "Assets",
      "Point",
      "extensions",
      "configureCubismSDK",
      "Live2DModel",
      "Live2DPlugin",
    ]) {
      if (!(key in vendor)) throw new Error(`Live2D vendor export is missing: ${key}`)
    }

    if (!vendor[VENDOR_READY]) {
      vendor.extensions.add(vendor.Live2DPlugin)
      await vendor.configureCubismSDK({ memorySizeMB: 64 })
      try {
        Object.defineProperty(vendor, VENDOR_READY, {
          value: true,
          configurable: true,
        })
      } catch {
        // A non-extensible vendor object can still be used for this install.
      }
    }

    vendor.Assets.setPreferences?.({
      preferCreateImageBitmap: false,
      preferWorkers: false,
    })

    bootStage = "decoding embedded model bundle"
    const preparedBundle = createBundleUrls(bundle)
    blobUrls = preparedBundle.urls
    if (disposed) throw new Error("Installation was disposed")

    bootStage = "initializing Pixi Application"
    const stagedApp = new vendor.Application()
    app = stagedApp
    await stagedApp.init({
      width: rootLogicalWidth,
      height: rootLogicalHeight,
      backgroundAlpha: 0,
      antialias: true,
      autoDensity: false,
      autoStart: false,
      sharedTicker: false,
      clearBeforeRender: true,
      preference: "webgl",
      powerPreference: "low-power",
      resolution: Math.min(2, Math.max(1, Number(globalScope.devicePixelRatio) || 1)),
    })
    if (disposed) {
      app = null
      try {
        stagedApp.destroy(
          { removeView: true },
          { children: true, texture: true, textureSource: true },
        )
      } catch {
        // The caller disposed while renderer initialization was in flight.
      }
      throw new Error("Installation was disposed")
    }

    app.ticker.maxFPS = effectiveFps
    app.ticker.add((ticker) => {
      if (active && !disposed) {
        frames += 1
        motionDirector?.tick(ticker.deltaMS)
        if (appearanceHitRefresh >= 0 && frames >= appearanceHitRefresh) { appearanceHitRefresh = -1; buildHitMask() }
        if (supplied.preview && !previewPlaying && previewFrames > 0 && --previewFrames === 0) updatePlayback()
      }
    })
    app.ticker.stop()

    canvas = app.canvas
    setCanvasStyle(canvas)
    contextLostHandler = (event) => {
      event.preventDefault?.()
      fail(new Error("WebGL context lost"))
    }
    canvas.addEventListener("webglcontextlost", contextLostHandler)
    mountCanvas(root)

    // Pixi's automatic loader cannot infer a parser from an extensionless
    // blob: URL. Load and cache textures explicitly on the main thread first;
    // Live2DModel.from then receives the cached Texture instances.
    bootStage = "loading textures on the main thread"
    const preloadedTextures = await Promise.all(
      preparedBundle.textureUrls.map((url) => vendor.Assets.load({
        src: url,
        parser: "loadTextures",
        data: { autoGenerateMipmaps: true },
      })),
    )
    if (preloadedTextures.some((texture) => !texture)) {
      throw new Error("Pixi returned an empty texture while preloading the Live2D bundle")
    }
    if (disposed) throw new Error("Installation was disposed")

    bootStage = "loading Live2D model"
    const loaded = await vendor.Live2DModel.from(preparedBundle.modelUrl, {
      autoUpdate: false,
      autoHitTest: false,
      autoFocus: false,
      ticker: app.ticker,
      textureOptions: { lod: "full" },
    })
    if (disposed) {
      try {
        loaded.destroy({ children: true, texture: true })
      } catch {
        // Disposal raced model loading.
      }
      throw new Error("Installation was disposed")
    }
    if (!app) {
      try {
        loaded.destroy({ children: true, texture: true })
      } catch {
        // The application was released while the model was loading.
      }
      throw new Error("Pixi Application was released before the model finished loading")
    }

    model = loaded
    app.stage.addChild(loaded)
    motionDirector = createMotionDirector(loaded, vendor)
    appearanceLayer = createAppearanceLayer(loaded)
    const intrinsic = loaded.internalModel
    sourceSize = {
      width: Math.max(1, Number(intrinsic?.originalWidth) || Number(loaded.width) || 1),
      height: Math.max(1, Number(intrinsic?.originalHeight) || Number(loaded.height) || 1),
    }
    modelReady = true
    layout()
    loaded.automator.autoUpdate = true

    renderOnce()
    buildHitMask()
    renderedOnce = true
    bootStage = "ready"
    activateIfMounted()
    controlsHover = createControlsHover()
    scheduleStateExpression()
    updatePlayback()
  }

  function ensureBoot() {
    if (disposed || bootAttempted) return bootPromise
    bootAttempted = true
    bootPromise = boot()
      .catch((reason) => {
        fail(reason)
      })
      .finally(() => {
        bootPromise = null
      })
    return bootPromise
  }

  function onMotionPreferenceChange() {
    effectiveFps = reducedMotionQuery?.matches
      ? Math.min(configuredFps, 12)
      : configuredFps
    if (reducedMotionQuery?.matches) setGaze(0, 0)
    updatePlayback()
  }

  function status() {
    const rect = root?.isConnected ? root.getBoundingClientRect() : null
    return {
      active,
      disposed,
      rendererBuildId: "codex-preview-1",
      state: forcedState ?? root?.getAttribute?.(STATE_ATTRIBUTE) ?? state,
      frames,
      rootConnected: Boolean(root?.isConnected),
      fps: effectiveFps,
      stage: bootStage,
      canvas: {
        width: canvas?.width || 0,
        height: canvas?.height || 0,
        cssWidth: Math.round(rect?.width || rootLogicalWidth),
        cssHeight: Math.round(rect?.height || rootLogicalHeight),
        resolution: Math.min(2, Math.max(1, Number(globalScope.devicePixelRatio) || 1)),
      },
      canvasWidth: canvas?.width || 0,
      canvasHeight: canvas?.height || 0,
      modelScale: model?.scale?.x || 0,
      sizeMultiplier: 1.06,
      drawingBounds: canvasMetrics(),
      hitArea: {regions: hitMask.length, error: hitAreaError},
      controlsHover: controlsHover?.status() || null,
      running: playbackRunning,
      lastError: lastError || null,
      lastWarning: lastWarning || null,
      expression: currentExpressionName,
      appearance,
      appearanceParameters: appearanceLayer?.status() || {},
      desktopEffect: desktopEffect?.id || null,
      previewPlaying: supplied.preview ? previewPlaying : null,
      motion: motionDirector?.status() || null,
    }
  }

  function setState(nextState) {
    const normalized = String(nextState || "")
    if (!KNOWN_STATES.has(normalized)) {
      throw new RangeError(`Unknown Codex pet state: ${normalized}`)
    }
    forcedState = normalized
    setStateInternal(normalized)
    return status()
  }

  function dispose() {
    if (disposed) return status()
    disposed = true
    documentObserver?.disconnect()
    rootObserver?.disconnect()
    resizeObserver?.disconnect()
    intersectionObserver?.disconnect()
    documentObserver = null
    rootObserver = null
    resizeObserver = null
    intersectionObserver = null

    globalScope.removeEventListener?.("pointermove", onPointerMove)
    globalScope.removeEventListener?.("pointerout", onPointerOut)
    documentScope?.removeEventListener?.("visibilitychange", onVisibilityChange)
    motionPreference?.removeEventListener?.("change", motionPreferenceHandler)
    motionPreference = null
    motionPreferenceHandler = null

    destroyEngine()
    root = null
    lastNativeState = null
    forcedState = null
    rootLogicalWidth = 1
    rootLogicalHeight = 1
    styleElement?.remove()
    styleElement = null
    return status()
  }

  const api = {
    status,
    setState,
    setAppearance,
    playEffect,
    setPreviewPlaying,
    setFps,
    playMotion(group) { return motionDirector?.play(String(group)) || false },
    restore: dispose,
    dispose,
  }
  globalScope.__codexLive2D = api

  if (!documentScope) {
    lastError = "This adapter must run in a browser document"
    return status()
  }

  globalScope.addEventListener?.("pointermove", onPointerMove, { passive: true })
  globalScope.addEventListener?.("pointerout", onPointerOut, { passive: true })
  documentScope.addEventListener?.("visibilitychange", onVisibilityChange)
  if (reducedMotionQuery?.addEventListener) {
    motionPreference = reducedMotionQuery
    motionPreferenceHandler = onMotionPreferenceChange
    motionPreference.addEventListener("change", motionPreferenceHandler)
  }

  startDocumentObserver()
  const initialRoot = findPetRoot()
  attachRoot(initialRoot)
  if (!initialRoot) {
    lastError = "Codex pet root not found; waiting for the native DOM."
    return status()
  }

  await ensureBoot()
  return status()
}
