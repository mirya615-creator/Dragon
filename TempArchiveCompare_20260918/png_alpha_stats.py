import glob, os, struct, sys, zlib


def decode_png(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    pos = 8
    idat = b''
    w = h = bitdepth = colortype = None
    while pos < len(data):
        length = struct.unpack('>I', data[pos:pos + 4])[0]
        ctype = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + length]
        if ctype == b'IHDR':
            w, h, bitdepth, colortype = struct.unpack('>IIBB', chunk[:10])
        elif ctype == b'IDAT':
            idat += chunk
        elif ctype == b'IEND':
            break
        pos += 12 + length
    raw = zlib.decompress(idat)
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[colortype]
    assert bitdepth == 8, bitdepth
    bpp = channels
    stride = w * bpp
    out = bytearray(h * stride)
    prev = bytearray(stride)
    p = 0
    for y in range(h):
        f = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        if f == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 0xFF
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif f == 3:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif f == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        out[y * stride:(y + 1) * stride] = line
        prev = line
    return w, h, channels, out


d = r'D:\Codex project\Dragon\Assets\DragonBound\UI\Variants\V2\Content\Resources\UIResources\Unit\LevelUp'
for f in sorted(glob.glob(os.path.join(d, '*.png'))):
    w, h, ch, px = decode_png(f)
    if ch < 4:
        print(f'{os.path.basename(f):45s} no alpha channel (ch={ch})'); continue
    alphas = px[3::4]
    n = len(alphas)
    mx = max(alphas)
    mean = sum(alphas) / n
    over8 = sum(1 for v in alphas if v > 8)
    minx, miny, maxx, maxy = w, h, -1, -1
    for y in range(h):
        row = alphas[y * w:(y + 1) * w]
        for x in range(w):
            if row[x] > 8:
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    bbox = 'none' if maxx < 0 else f'({minx},{miny})-({maxx},{maxy}) {maxx-minx+1}x{maxy-miny+1}'
    print(f'{os.path.basename(f):45s} {w}x{h} alphaMax={mx:3d} alphaMean={mean:6.1f} >8:{over8*100/n:5.1f}% bbox={bbox}')
