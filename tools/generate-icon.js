// Generates app.ico (16/32/48/256 px) from an SVG file
// Usage: node generate-icon.js <input.svg> <output.ico>
// Requires: sharp (npm install sharp)

const sharp = require('sharp');
const fs    = require('fs');

const [,, svgPath, outPath] = process.argv;
if (!svgPath || !outPath) {
    console.error('Usage: node generate-icon.js <input.svg> <output.ico>');
    process.exit(1);
}

// Strip white background rects added by design tools (Canva etc.)
let svg = fs.readFileSync(svgPath, 'utf8');
svg = svg.replace(/<rect[^>]*fill="#ffffff"[^/]*\/>/g, '');

const sizes = [256, 48, 32, 16];

async function main() {
    const pngs = await Promise.all(sizes.map(size =>
        sharp(Buffer.from(svg))
            .resize(size, size, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
            .png()
            .toBuffer()
    ));

    const count      = sizes.length;
    const dataOffset = 6 + count * 16;
    const parts      = [Buffer.from([0, 0, 1, 0, count, 0])];

    let offset = dataOffset;
    for (let i = 0; i < count; i++) {
        const w   = sizes[i] >= 256 ? 0 : sizes[i];
        const len = pngs[i].length;
        const e   = Buffer.alloc(16);
        e.writeUInt8(w, 0);  e.writeUInt8(w, 1);
        e.writeUInt16LE(1, 4);  e.writeUInt16LE(32, 6);
        e.writeUInt32LE(len, 8);  e.writeUInt32LE(offset, 12);
        parts.push(e);
        offset += len;
    }
    for (const buf of pngs) parts.push(buf);

    const ico = Buffer.concat(parts);
    fs.writeFileSync(outPath, ico);
    console.log(`Generated: ${outPath} (${Math.round(ico.length / 1024)} KB)`);
}

main().catch(err => { console.error(err); process.exit(1); });
