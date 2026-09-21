import zlib, struct, sys

def read_png(path):
    data = open(path,'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    pos = 8; idat = b''; w=h=bd=ct=None; plte=None; trns=None
    while pos < len(data):
        ln = struct.unpack('>I', data[pos:pos+4])[0]; typ = data[pos+4:pos+8]
        chunk = data[pos+8:pos+8+ln]; pos += 12+ln
        if typ == b'IHDR': w,h,bd,ct,comp,filt,inter = struct.unpack('>IIBBBBB', chunk)
        elif typ == b'IDAT': idat += chunk
        elif typ == b'PLTE': plte = chunk
        elif typ == b'tRNS': trns = chunk
    raw = zlib.decompress(idat)
    channels = {0:1,2:3,3:1,4:2,6:4}[ct]
    bpp = channels * bd // 8
    stride = w*bpp
    out = bytearray(h*stride); prev = bytearray(stride)
    p = 0
    for y in range(h):
        f = raw[p]; p += 1
        line = bytearray(raw[p:p+stride]); p += stride
        if f == 1:
            for i in range(bpp, stride): line[i] = (line[i] + line[i-bpp]) & 255
        elif f == 2:
            for i in range(stride): line[i] = (line[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                a = line[i-bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i-bpp] if i >= bpp else 0
                b = prev[i]; c = prev[i-bpp] if i >= bpp else 0
                pp = a+b-c
                pa, pb, pc = abs(pp-a), abs(pp-b), abs(pp-c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        out[y*stride:(y+1)*stride] = line
        prev = line
    return w,h,ct,bpp,bytes(out)

def alpha_at(w,ct,bpp,px,x,y):
    if ct == 6: return px[(y*w+x)*4+3]
    if ct == 4: return px[(y*w+x)*4+1]
    if ct == 2: return 255
    if ct == 3: return 255
    return 255

path = sys.argv[1]
w,h,ct,bpp,px = read_png(path)
print("size", w, h, "colortype", ct)
# rows with visible pixels
rows = []
for y in range(h):
    cnt = 0
    for x in range(w):
        if alpha_at(w,ct,bpp,px,x,y) > 40: cnt += 1
    if cnt: rows.append((y,cnt))
if rows:
    print("visible rows: first=%d last=%d count=%d" % (rows[0][0], rows[-1][0], len(rows)))
    # print bands
    start = prev = rows[0][0]; mx = 0
    bands = []
    for y,c in rows:
        mx = max(mx,c)
        if y != prev+1:
            bands.append((start,prev,mx)); start = y; mx = c
        prev = y
    bands.append((start,prev,mx))
    for b in bands:
        print("band y %d..%d (h=%d) maxSolidPx=%d" % (b[0],b[1],b[1]-b[0]+1,b[2]))
