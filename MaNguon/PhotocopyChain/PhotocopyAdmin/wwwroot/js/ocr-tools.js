const editors = new WeakMap();
const clamp = v => Math.max(0, Math.min(1, v));
export function normalizeRect(a, b) {
    const x = Math.min(clamp(a[0]), clamp(b[0])), y = Math.min(clamp(a[1]), clamp(b[1]));
    const width = Math.abs(clamp(a[0]) - clamp(b[0])), height = Math.abs(clamp(a[1]) - clamp(b[1]));
    return width < .005 || height < .005 ? null : [x, y, width, height];
}
function paint(canvas, state) {
    const ctx = canvas.getContext('2d'), [x,y,w,h] = state.rect;
    const left=x*canvas.width, top=y*canvas.height, width=w*canvas.width, height=h*canvas.height;
    ctx.clearRect(0,0,canvas.width,canvas.height); ctx.drawImage(state.image,0,0,canvas.width,canvas.height);
    ctx.fillStyle='rgba(10,25,40,.57)';
    ctx.fillRect(0,0,canvas.width,top);ctx.fillRect(0,top+height,canvas.width,canvas.height-top-height);
    ctx.fillRect(0,top,left,height);ctx.fillRect(left+width,top,canvas.width-left-width,height);
    ctx.strokeStyle='#fff';ctx.lineWidth=3;ctx.setLineDash([]);ctx.strokeRect(left,top,width,height);
    ctx.strokeStyle='#155f9b';ctx.lineWidth=1.5;ctx.setLineDash([6,4]);ctx.strokeRect(left,top,width,height);ctx.setLineDash([]);
}
export async function initCrop(canvas, source, rect) {
    const image = new Image(); image.src=source; await image.decode();
    if (image.naturalWidth * image.naturalHeight > 20000000) throw new Error('Ảnh vượt giới hạn 20 megapixel.');
    const ratio=Math.min(1,900/image.naturalWidth,Math.max(160,window.innerHeight*.53)/image.naturalHeight);
    canvas.width=Math.max(1,Math.round(image.naturalWidth*ratio));canvas.height=Math.max(1,Math.round(image.naturalHeight*ratio));
    const state={image,rect:rect||[0,0,1,1],anchor:null,previous:null};editors.set(canvas,state);
    const point=e=>{const b=canvas.getBoundingClientRect();return [clamp((e.clientX-b.left)/b.width),clamp((e.clientY-b.top)/b.height)];};
    canvas.onpointerdown=e=>{if(e.button!==0)return;e.preventDefault();state.anchor=point(e);state.previous=state.rect;canvas.setPointerCapture(e.pointerId);};
    canvas.onpointermove=e=>{if(!state.anchor)return;const r=normalizeRect(state.anchor,point(e));if(r){state.rect=r;paint(canvas,state);}};
    canvas.onpointerup=e=>{if(!state.anchor)return;state.rect=normalizeRect(state.anchor,point(e))||state.previous;state.anchor=null;paint(canvas,state);if(canvas.hasPointerCapture(e.pointerId))canvas.releasePointerCapture(e.pointerId);};
    canvas.onpointercancel=()=>{if(state.anchor){state.rect=state.previous;state.anchor=null;paint(canvas,state);}};
    paint(canvas,state);
}
export function resetCrop(canvas) {const state=editors.get(canvas);state.rect=[0,0,1,1];paint(canvas,state);}
export function cropSelection(canvas) {return editors.get(canvas).rect;}
export function cropPreview(canvas) {
    const {image,rect:[x,y,w,h]}=editors.get(canvas), width=w*image.naturalWidth, height=h*image.naturalHeight;
    if(width<8||height<8)throw new Error('Vùng cắt quá nhỏ.');
    const target=document.createElement('canvas'),scale=Math.min(1,1600/Math.max(width,height));
    target.width=Math.max(1,Math.round(width*scale));target.height=Math.max(1,Math.round(height*scale));
    const ctx=target.getContext('2d');ctx.fillStyle='#fff';ctx.fillRect(0,0,target.width,target.height);
    ctx.drawImage(image,x*image.naturalWidth,y*image.naturalHeight,width,height,0,0,target.width,target.height);
    return target.toDataURL('image/png');
}
export function disposeCrop(canvas) {if(!canvas)return;canvas.onpointerdown=canvas.onpointermove=canvas.onpointerup=canvas.onpointercancel=null;editors.delete(canvas);}
export async function downloadWord(stream) {
    const buffer=await stream.arrayBuffer();
    const url=URL.createObjectURL(new Blob([buffer],{type:'application/vnd.openxmlformats-officedocument.wordprocessingml.document'}));
    const a=document.createElement('a');a.href=url;a.download='Van-ban-nhan-dang.docx';document.body.appendChild(a);a.click();a.remove();
    setTimeout(()=>URL.revokeObjectURL(url),30000);
}
