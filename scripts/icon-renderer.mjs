// Self-contained because build-branding serializes this into an isolated canvas.
// The portrait and alpha silhouette stay unchanged; only small-frame RGB gets
// the mild local-contrast treatment selected in the actual-pixel comparison.
export function renderPortraitIcon(source,size,alreadyProcessed=false) {
  const canvas=document.createElement('canvas');canvas.width=canvas.height=size;
  const context=canvas.getContext('2d',{willReadFrequently:true});context.imageSmoothingEnabled=true;context.imageSmoothingQuality='high';context.drawImage(source,0,0,size,size);
  if(size<=64&&!alreadyProcessed){
    const image=context.getImageData(0,0,size,size),original=new Uint8ClampedArray(image.data),weights=[1,2,1];
    for(let y=0;y<size;y++)for(let x=0;x<size;x++){
      const i=(y*size+x)*4;if(original[i+3]<128)continue;
      const blur=[0,0,0];let weight=0;
      for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
        const sy=Math.min(size-1,Math.max(0,y+dy)),sx=Math.min(size-1,Math.max(0,x+dx)),j=(sy*size+sx)*4,w=weights[dy+1]*weights[dx+1]*original[j+3]/255;
        weight+=w;for(let k=0;k<3;k++)blur[k]+=original[j+k]*w;
      }
      const rgb=[0,1,2].map(k=>Math.max(0,Math.min(255,((original[i+k]+1.1*(original[i+k]-blur[k]/Math.max(weight,.001)))-128)*1.02+128)));
      const luma=.2126*rgb[0]+.7152*rgb[1]+.0722*rgb[2];
      for(let k=0;k<3;k++)image.data[i+k]=Math.max(0,Math.min(255,luma+(rgb[k]-luma)*1.02));
    }
    context.putImageData(image,0,0);
  }
  return canvas;
}
