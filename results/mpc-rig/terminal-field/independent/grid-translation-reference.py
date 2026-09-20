"""Independent numerical probe, not Unity test evidence or acceptance validation."""
import csv
import heapq
import math
from pathlib import Path

root = Path(__file__).resolve().parent
n = 48
goal = (0, 90)
rocks = [(-14,25),(6,30),(-4,42),(14,48),(-12,55),(4,63),(-6,72),(10,78)]

def bake(ship_x, snapped):
    h = max(4, (35 + 25 * (1.7 + .4) + 1.3) / (23.5 - 2))
    origin = (ship_x / 2 - h * 23.5, 55 - h * 23.5)
    if snapped:
        origin = tuple(round(v / h) * h for v in origin)
    centers = [(origin[0] + h * (i % n), origin[1] + h * (i // n)) for i in range(n*n)]
    occupied = [any((x-rx)**2 + (y-ry)**2 <= 3.8**2 for rx,ry in rocks) for x,y in centers]
    seed = min((i for i in range(n*n) if not occupied[i]), key=lambda i: (math.dist(centers[i],goal),i))
    distances = [math.inf] * (n*n)
    distances[seed] = math.dist(centers[seed],goal)
    queue = [(distances[seed],seed)]
    while queue:
        d,i = heapq.heappop(queue)
        if d != distances[i]:
            continue
        x,y = i%n,i//n
        for dy in (-1,0,1):
            for dx in (-1,0,1):
                if not (dx or dy) or not (0 <= x+dx < n and 0 <= y+dy < n):
                    continue
                j = i + dx + n*dy
                if occupied[j] or (dx and dy and (occupied[i+dx] or occupied[i+n*dy])):
                    continue
                candidate = d + h * (math.sqrt(2) if dx and dy else 1)
                if candidate < distances[j]:
                    distances[j] = candidate
                    heapq.heappush(queue,(candidate,j))
    unreachable = max(d for d in distances if math.isfinite(d)) + h*(n-1)*math.sqrt(2)
    def excess(i):
        if not math.isfinite(distances[i]):
            return unreachable
        a,b = abs(i%n-seed%n), abs(i//n-seed//n)
        empty = h*(max(a,b)+(math.sqrt(2)-1)*min(a,b)) + math.dist(centers[seed],goal)
        return max(0,distances[i]-empty)
    def sample(point):
        gx,gy = ((point[k]-origin[k])/h for k in (0,1))
        x,y = math.floor(gx),math.floor(gy)
        fx,fy = gx-x,gy-y
        i=x+n*y
        return (1-fy)*((1-fx)*excess(i)+fx*excess(i+1)) + fy*((1-fx)*excess(i+n)+fx*excess(i+n+1))
    return h,origin,sum(occupied),[sample(p) for p in [(0,20),(0,21),(0,35)]]

with (root/'grid-translation-reference.csv').open('w',newline='',encoding='utf-8') as stream:
    writer=csv.writer(stream)
    writer.writerow(['snapped','shipX','originX','originY','spacing','occupiedCells','atStart','oneMetreAhead','atFixedEndpoint'])
    for snapped in (False,True):
        measurements=[]
        for step in range(81):
            h,origin,occupied,samples=bake(step*.1,snapped)
            writer.writerow([snapped,step*.1,*origin,h,occupied,*samples])
            measurements.append(samples)
        print({'snapped':snapped,'maximum_sample_change_per_0_1m_ship_shift':max(abs(a-b) for prev,next_ in zip(measurements,measurements[1:]) for a,b in zip(prev,next_)),
               'fixed_endpoint_range': [min(m[2] for m in measurements),max(m[2] for m in measurements)],
               'startup_descent_range':[min(m[0]-m[1] for m in measurements),max(m[0]-m[1] for m in measurements)]})
