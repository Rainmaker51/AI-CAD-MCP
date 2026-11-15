"""Synthetic, dimensioned P5 corpus; never described as a real project drawing."""
import pathlib,json,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0,str(ROOT/'.tools/python-packages'))
from reportlab.pdfgen import canvas
from PIL import Image
import cv2,numpy as np
folder=ROOT/'tests/fixtures/intelligence';folder.mkdir(parents=True,exist_ok=True)
pdf=folder/'floorplan-vector.pdf';c=canvas.Canvas(str(pdf),pagesize=(650,500));c.setTitle('AI CAD P5 - synthetic dimensioned floorplan')
c.setFont('Helvetica-Bold',15);c.drawString(55,460,'P5 TEST FIXTURE / NOT A PROJECT DRAWING')
c.setFont('Helvetica',10);c.drawString(55,442,'Known outline 6000 x 4200 mm; door D01 = 900 mm; walls 200 mm.')
x,y,s=80,80,.06
walls=[(0,0,200,4200),(5800,0,200,4200),(0,4000,6000,200),(0,0,2000,200),(2900,0,3100,200),(3000,200,200,2100),(3000,3200,200,800)]
for a,b,w,h in walls:c.rect(x+a*s,y+b*s,w*s,h*s,fill=1,stroke=0)
c.setLineWidth(.8);c.line(x+2000*s,y+200*s,x+2000*s,y+1100*s);c.arc(x+1100*s,y-700*s,x+2900*s,y+1100*s,startAng=0,extent=90)
c.setFont('Helvetica',11);c.drawString(x+2100*s,y+1250*s,'D01 900');c.drawString(x+700*s,y+2600*s,'LIVING');c.drawString(x+3800*s,y+2600*s,'ROOM')
c.setDash(6,3);c.line(x+1000*s,y+200*s,x+1000*s,y+3800*s);c.setDash();c.circle(x+1000*s,y+3700*s,8);c.drawCentredString(x+1000*s,y+3700*s-4,'1')
c.setLineWidth(.5);c.line(x,y+4500*s,x+6000*s,y+4500*s);c.line(x,y+4400*s,x,y+4600*s);c.line(x+6000*s,y+4400*s,x+6000*s,y+4600*s);c.drawCentredString(x+3000*s,y+4600*s,'6000')
c.setFont('Helvetica',9);c.drawString(55,42,'Generated acceptance data. Semantic candidates and OCR still require review.')
c.showPage();c.save()
renderer=pathlib.Path(sys.executable).parent.parent/'native/poppler/Library/bin/pdftoppm.exe'
subprocess.run([str(renderer),'-singlefile','-r','144','-png',str(pdf),str(folder/'floorplan-render')],check=True)
image=cv2.imread(str(folder/'floorplan-render.png'));height,width=image.shape[:2];angle=1.5;matrix=cv2.getRotationMatrix2D((width/2,height/2),angle,1)
scan=cv2.warpAffine(image,matrix,(width,height),borderValue=(255,255,255));rng=np.random.default_rng(7);scan=np.clip(scan.astype(np.float32)+rng.normal(0,2,scan.shape),0,255).astype(np.uint8);cv2.imwrite(str(folder/'floorplan-scan.png'),scan)
def transform(p):a=matrix@np.array([p[0],p[1],1]);return {'x':float(a[0]),'y':float(a[1])}
manifest={'synthetic':True,'widthMm':6000,'heightMm':4200,'walls':walls,'vector':{'file':pdf.name,'calibration':{'p1':{'x':80,'y':168},'p2':{'x':440,'y':168},'distanceMm':6000,'confirmed':True}},'scan':{'file':'floorplan-scan.png','skewDegrees':angle,'calibration':{'p1':transform((160,336)),'p2':transform((880,336)),'distanceMm':6000,'confirmed':True}}}
(folder/'corpus.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(folder)
