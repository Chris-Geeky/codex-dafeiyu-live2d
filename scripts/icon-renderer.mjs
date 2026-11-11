// Both functions are serialized into the isolated branding canvas.
export function portraitBounds(source) {
  const canvas=document.createElement('canvas');
  const width=canvas.width=source.naturalWidth,height=canvas.height=source.naturalHeight;
  const context=canvas.getContext('2d',{willReadFrequently:true});
  context.drawImage(source,0,0);
  const {data}=context.getImageData(0,0,width,height);
  let left=width,top=height,right=-1,bottom=-1;
  for(let y=0;y<height;y++)for(let x=0;x<width;x++){
    if(!data[(y*width+x)*4+3])continue;
    left=Math.min(left,x);top=Math.min(top,y);right=Math.max(right,x);bottom=Math.max(bottom,y);
  }
  if(right<left)throw new Error('The icon source is fully transparent.');
  return {left,top,width:right-left+1,height:bottom-top+1};
}

export function renderPortraitIcon(source,size,alreadyProcessed=false,bounds=null) {
  const canvas=document.createElement('canvas');canvas.width=canvas.height=size;
  const context=canvas.getContext('2d',{willReadFrequently:true});context.imageSmoothingEnabled=true;context.imageSmoothingQuality='high';
  if(bounds){
    // Fill the Windows icon slot with the whole silhouette, leaving a small
    // inset for antialiasing. Keep the original proportions and ear tips.
    const inset=Math.max(.5,size*.01);
    const scale=(size-2*inset)/Math.max(bounds.width,bounds.height);
    const width=bounds.width*scale,height=bounds.height*scale;
    context.drawImage(source,bounds.left,bounds.top,bounds.width,bounds.height,(size-width)/2,(size-height)/2,width,height);
  }else{
    context.drawImage(source,0,0,size,size);
  }
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
