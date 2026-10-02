from PIL import Image
import sys, os
os.chdir(os.path.dirname(os.path.abspath(__file__)))
n = ['front', 'left', 'right', 'top', 'rear', 'persp']
ims = [Image.open(f'view_{x}.png') for x in n]
w, h = ims[0].size
s = Image.new('RGB', (w * 3, h * 2))
for i, im in enumerate(ims):
    s.paste(im, ((i % 3) * w, (i // 3) * h))
s.save(sys.argv[1] if len(sys.argv) > 1 else 'sheet.png')
