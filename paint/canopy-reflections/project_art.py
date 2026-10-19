import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).parent
SIZE = 512

def array(items, keys):
    return np.array([[v[k] for k in keys] for v in items], dtype=np.float64)

def load_mesh(ship):
    data = json.loads((OUT / (ship + '-unity.json')).read_text())
    co, uv = array(data['vertices'], 'xyz'), array(data['uv'], 'xy')
    triangles = np.array(data['triangles']).reshape(-1,3)
    if ship == 'Crimson':
        source = np.load(OUT / 'Crimson-Canopy.npz')
        target = source['co'] * [-1,1,1]
        distance = np.full(len(co), np.inf)
        for point in target:
            distance = np.minimum(distance, np.linalg.norm(co-point,axis=1))
        triangles = triangles[np.all(distance[triangles] < 1e-5, axis=1)]
        assert len(triangles) == 236, len(triangles)
    return co, uv, triangles

def sample(image, uv):
    height, width = image.shape[:2]
    x = np.clip(uv[...,0]*width-.5,0,width-1)
    y = np.clip(uv[...,1]*height-.5,0,height-1)
    x0,y0 = x.astype(int),y.astype(int)
    x1,y1 = np.minimum(x0+1,width-1),np.minimum(y0+1,height-1)
    fx,fy = (x-x0)[...,None],(y-y0)[...,None]
    return ((image[y0,x0]*(1-fx)+image[y0,x1]*fx)*(1-fy)
            +(image[y1,x0]*(1-fx)+image[y1,x1]*fx)*fy)

def project(co, uv, triangles):
    points = co[triangles].reshape(-1,3)
    low, high = points.min(0), points.max(0)
    xy = (co[:,:2]-low[:2])/(high-low)[:2] * (SIZE-1)
    tex_uv = np.zeros((2,SIZE,SIZE,2))
    coverage = np.zeros((2,SIZE,SIZE),bool)
    depths = np.full((2,SIZE,SIZE),-np.inf)
    for ids in triangles:
        a,b,c = xy[ids]
        den = np.cross(b-a,c-a)
        if abs(den) < 1e-9: continue
        x0,y0 = np.maximum(np.floor(np.minimum.reduce([a,b,c])).astype(int),0)
        x1,y1 = np.minimum(np.ceil(np.maximum.reduce([a,b,c])).astype(int),SIZE-1)
        x,y = np.meshgrid(np.arange(x0,x1+1),np.arange(y0,y1+1))
        px,py = x-a[0],y-a[1]
        wb = (px*(c-a)[1]-py*(c-a)[0])/den
        wc = ((b-a)[0]*py-(b-a)[1]*px)/den
        wa = 1-wb-wc
        inside = (wa>=-1e-6)&(wb>=-1e-6)&(wc>=-1e-6)
        z = wa*co[ids[0],2]+wb*co[ids[1],2]+wc*co[ids[2],2]
        mapped = wa[...,None]*uv[ids[0]]+wb[...,None]*uv[ids[1]]+wc[...,None]*uv[ids[2]]
        for side,sign in enumerate((1,-1)):
            previous = depths[side,y0:y1+1,x0:x1+1]
            visible = inside & (sign*z>previous)
            previous[visible] = sign*z[visible]
            tex_uv[side,y0:y1+1,x0:x1+1][visible] = mapped[visible]
            coverage[side,y0:y1+1,x0:x1+1][visible] = True
    return tex_uv,coverage,low,high

def image(path):
    return np.asarray(Image.open(path).convert('RGB'),np.float64)[::-1]/255

def main():
    for ship in ('Crimson','Nightshade','Valis'):
        co,uv,tris=load_mesh(ship)
        mapped,covered,low,high=project(co,uv,tris)
        np.savez(OUT/(ship+'-projection.npz'),uv=mapped,coverage=covered,low=low,high=high)
        sources = {'Color': ROOT/'art/ships/crimson/textures/Crimson_BaseColor.png'} if ship=='Crimson' else {
            mask:ROOT/'art/ships'/ship.lower()/'layers'/(mask+'.png') for mask in ('Shadow','Light','Ink')}
        sheet=Image.new('RGB',(len(sources)*SIZE,2*SIZE+60),(24,26,30))
        draw=ImageDraw.Draw(sheet)
        for column,(name,path) in enumerate(sources.items()):
            pixels=sample(image(path),mapped)
            pixels[~covered]=0
            np.save(OUT/(ship+'-'+name+'.npy'),pixels)
            for side in range(2):
                tile=Image.fromarray(np.rint(np.clip(pixels[side],0,1)*255).astype('uint8')[::-1])
                sheet.paste(tile,(column*SIZE,30+side*(SIZE+15)))
                draw.text((column*SIZE+8,side*(SIZE+15)+8),f'{ship}: {name}, object Z {"+" if side==0 else "-"}',fill='white')
        sheet.save(OUT/(ship+'-projected.png'))
        print(ship, 'bounds',low,high)

if __name__=='__main__': main()
