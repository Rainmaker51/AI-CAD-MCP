"""Local vector/PDF/raster intake. Never guesses the authoritative drawing scale."""
import argparse, hashlib, json, math, pathlib, sys, time
ROOT=pathlib.Path(__file__).resolve().parents[2]
packages=ROOT/'storage/03-python-ocr/python-packages'
if not packages.is_dir():
 packages=ROOT/'.tools/python-packages'
sys.path.insert(0,str(packages))
import numpy as np
import cv2
from PIL import Image,ImageDraw

def analyze(request):
 path=pathlib.Path(request['path']).resolve()
 if not path.is_file() or path.stat().st_size>20*1024*1024:raise ValueError('Source missing or exceeds 20 MB')
 digest=hashlib.sha256(path.read_bytes()).hexdigest()
 if digest!=request['hash']:raise ValueError('Source hash changed')
 cal=request.get('calibration',{})
 if cal.get('confirmed') is not True:raise ValueError('NEEDS_CALIBRATION: two verified points and distance are required')
 p1,p2=cal['p1'],cal['p2'];dx=p2['x']-p1['x'];dy=p2['y']-p1['y'];length=math.hypot(dx,dy);known=float(cal['distanceMm'])
 if not 1<=length<=100000 or not 1<=known<=1000000:raise ValueError('Invalid reference distance')
 scale=known/length;angle=math.atan2(dy,dx);cos,sin=math.cos(angle),math.sin(angle)
 def point(x,y):
  a,b=x-p1['x'],y-p1['y'];return {'X':round((a*cos+b*sin)*scale,4),'Y':round((a*sin-b*cos)*scale,4)}
 model={'schemaVersion':1,'sourceId':request['sourceId'],'sourceHash':digest,'page':int(request.get('page',0)),'units':'mm','calibration':cal,'scaleMmPerSourceUnit':scale,'elements':[],'texts':[],'uncertainties':[],'metrics':{},'status':'REVIEW_REQUIRED'}
 def element(kind,points,confidence,region,reason=''):
  if len(model['elements'])>=1000:return
  model['elements'].append({'id':f'E{len(model["elements"])+1:04}','kind':kind,'points':points,'confidence':confidence,'sourceRegion':region,'needsReview':confidence<.98,'reason':reason})
 page_index=model['page'];image=None;vector=False
 if path.suffix.lower()=='.pdf':
  import pdfplumber
  with pdfplumber.open(path) as pdf:
   if page_index<0 or page_index>=min(100,len(pdf.pages)):raise ValueError('Invalid PDF page')
   page=pdf.pages[page_index]
   if page.width>10000 or page.height>10000:raise ValueError('Oversized PDF page')
   words=page.extract_words() or [];vector=bool(page.lines or page.rects or page.curves)
   image_area=sum(float(im.get('width',0))*float(im.get('height',0)) for im in page.images)
   if image_area>page.width*page.height*.2:
    model['hybridVectorDetected']=vector;vector=False
   model['pageSize']={'width':page.width,'height':page.height,'coordinateSystem':'PDF points, top-left'}
   for word in words[:500]:model['texts'].append({'text':word['text'],'confidence':1.0,'position':point(word['x0'],word['top']),'sourceRegion':[word['x0'],word['top'],word['x1'],word['bottom']]})
   if vector:
    model['sourceType']='PDF_VECTOR'
    for r in page.rects[:500]:
     w,h=r['width']*scale,r['height']*scale
     kind='column' if 150<=w<=800 and 150<=h<=800 and max(w,h)/max(1,min(w,h))<1.8 else 'wall' if min(w,h)<=500 and max(w,h)>min(w,h)*2 else 'outline'
     points=[point(r['x0'],r['top']),point(r['x1'],r['top']),point(r['x1'],r['bottom']),point(r['x0'],r['bottom'])]
     element(kind,points,.99 if kind in ('wall','column') else .85,[r['x0'],r['top'],r['x1'],r['bottom']],'Geometry is vector-exact; semantic class requires review' if kind=='outline' else '')
    for line in page.lines[:500]:
     x0,y0,x1,y1=line['x0'],line['top'],line['x1'],line['bottom']
     if math.hypot(x1-x0,y1-y0)*scale<100:continue
     dashed=bool(line.get('dash') and line['dash'][0]);kind='axis' if dashed else 'line'
     element(kind,[point(x0,y0),point(x1,y1)],.85,[x0,y0,x1,y1],'Confirm line class; dimension/annotation lines may be included')
    for curve in page.curves[:100]:
     w,h=curve['width']*scale,curve['height']*scale
     if 400<=max(w,h)<=1500 and min(w,h)>200:element('door_candidate',[point(curve['x0'],curve['top']),point(curve['x1'],curve['bottom'])],.65,[curve['x0'],curve['top'],curve['x1'],curve['bottom']],'Curved vector candidate: confirm door size, hinge and orientation before block mapping')
   else:
    # Rendering is done with Poppler; no arbitrary renderer command comes from input.
    import subprocess,shutil
    executable=request.get('pdftoppm') or shutil.which('pdftoppm')
    if not executable:raise ValueError('PDF raster rendering requires Poppler')
    out=path.parent/(request['sourceId']+'.page')
    subprocess.run([executable,'-f',str(page_index+1),'-singlefile','-r','144','-png',str(path),str(out)],check=True,timeout=40,capture_output=True)
    image=Image.open(str(out)+'.png').convert('RGB');model['renderScale']=2.0
    original_point=point;point=lambda x,y:original_point(x/2,y/2)
 if not vector:
  model['sourceType']='PDF_SCAN' if path.suffix.lower()=='.pdf' else 'RASTER'
  if image is None:image=Image.open(path).convert('RGB')
  if image.width*image.height>20_000_000:raise ValueError('Image exceeds 20 megapixels')
  model.setdefault('pageSize',{'width':image.width,'height':image.height,'coordinateSystem':'pixels, top-left'})
  factor=min(1.0,1800/max(image.size));resized=image.resize((int(image.width*factor),int(image.height*factor)))
  rgb=np.asarray(resized)
  deskew=cv2.getRotationMatrix2D((rgb.shape[1]/2,rgb.shape[0]/2),math.degrees(angle),1)
  inverse=cv2.invertAffineTransform(deskew)
  rgb=cv2.warpAffine(rgb,deskew,(rgb.shape[1],rgb.shape[0]),borderValue=(255,255,255))
  raw_point=point
  def point(x,y):
   original=inverse@np.array([x*factor,y*factor,1.0]);return raw_point(float(original[0])/factor,float(original[1])/factor)
  model['preprocess']={'calibrationDeskewDegrees':math.degrees(angle),'matrix':deskew.tolist(),'coordinateSystem':'deskewed pixels at analysis resolution'}
  gray=cv2.cvtColor(rgb,cv2.COLOR_RGB2GRAY)
  _,binary=cv2.threshold(gray,0,255,cv2.THRESH_BINARY_INV|cv2.THRESH_OTSU)
  from rapidocr_onnxruntime import RapidOCR
  engine=RapidOCR();ocr,_=engine(cv2.cvtColor(rgb,cv2.COLOR_RGB2BGR))
  for box,text,confidence in (ocr or [])[:500]:
   xs=[float(v[0])/factor for v in box];ys=[float(v[1])/factor for v in box]
   model['texts'].append({'text':text,'confidence':float(confidence),'position':point(min(xs),min(ys)),'sourceRegion':[min(xs),min(ys),max(xs),max(ys)]})
   polygon=np.array(box,dtype=np.int32);cv2.fillPoly(binary,[polygon],0)
  horizontal=cv2.morphologyEx(binary,cv2.MORPH_OPEN,np.ones((3,45),np.uint8))
  vertical=cv2.morphologyEx(binary,cv2.MORPH_OPEN,np.ones((45,3),np.uint8))
  wall_mask=np.zeros_like(binary)
  for directional in (horizontal,vertical):
   contours,_=cv2.findContours(directional,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE)
   for contour in contours:
    x,y,w,h=cv2.boundingRect(contour);mw,mh=w*scale/factor,h*scale/factor
    if not(80<=min(mw,mh)<=500 and max(mw,mh)>=min(mw,mh)*2.5):continue
    box=[point(x/factor,y/factor),point((x+w)/factor,y/factor),point((x+w)/factor,(y+h)/factor),point(x/factor,(y+h)/factor)]
    element('wall',box,.92,[x/factor,y/factor,(x+w)/factor,(y+h)/factor],'Raster wall contour: verify thickness and junctions')
    cv2.drawContours(wall_mask,[contour],-1,255,cv2.FILLED)
  leftover=cv2.bitwise_and(binary,cv2.bitwise_not(cv2.dilate(wall_mask,np.ones((5,5),np.uint8))))
  lines=cv2.HoughLinesP(leftover,1,np.pi/180,25,minLineLength=40,maxLineGap=15)
  angles=[];segments=[]
  for line in ([] if lines is None else lines[:300]):
   x0,y0,x1,y1=map(float,line[0]);a=math.degrees(math.atan2(y1-y0,x1-x0));norm=((a+45)%90)-45
   if abs(norm)<12:angles.append(norm)
   distance=math.hypot(x1-x0,y1-y0)*scale/factor
   if distance<200:continue
   coords=(x0/factor,y0/factor,x1/factor,y1/factor)
   if any(max(abs(v-w) for v,w in zip(coords,previous))<8/factor for previous in segments):continue
   segments.append(coords)
   element('axis' if distance>2000 else 'line',[point(coords[0],coords[1]),point(coords[2],coords[3])],.78,list(coords),'Raster line: confirm axis/annotation class and endpoints')
  model['metrics']['detectedSkewDegrees']=round(float(np.median(angles)),3) if angles else None
  # Text detections are evidence, not authoritative design dimensions.
  for t in model['texts']:
   if any(marker in t['text'].upper() for marker in ['D01','DOOR']):model['uncertainties'].append({'kind':'door_label','text':t['text'],'confidence':t['confidence'],'reason':'Confirm hinge, opening and size before block placement'})
 model['uncertainties'] += [{'elementId':e['id'],'reason':e['reason'],'confidence':e['confidence']} for e in model['elements'] if e['needsReview']]
 if not model['elements']:raise ValueError('No usable geometry detected. Select another page/region or supply DWG reference.')
 all_points=[p for e in model['elements'] for p in e['points']]
 model['bounds']={'minX':min(p['X'] for p in all_points),'minY':min(p['Y'] for p in all_points),'maxX':max(p['X'] for p in all_points),'maxY':max(p['Y'] for p in all_points)}
 model['metrics'].update({'elementCount':len(model['elements']),'ocrTextCount':len(model['texts']),'referenceLengthMm':known,'referenceResidualMm':abs(length*scale-known),'uncertainCount':len(model['uncertainties'])})
 return model

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--request',required=True);parser.add_argument('--output',required=True);args=parser.parse_args();started=time.time()
 try:
  req=json.loads(pathlib.Path(args.request).read_text(encoding='utf-8-sig'));model=analyze(req);model['metrics']['durationSeconds']=round(time.time()-started,3);pathlib.Path(args.output).write_text(json.dumps(model,ensure_ascii=False,indent=2),encoding='utf-8')
 except Exception as e:print(json.dumps({'code':'INTELLIGENCE_FAILED','message':str(e)}),file=sys.stderr);raise SystemExit(1)
if __name__=='__main__':main()
