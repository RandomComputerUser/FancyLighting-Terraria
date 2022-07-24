function main()
{
    const canvas = document.getElementById('output');
    const ctx = canvas.getContext('2d');

    canvas.width = 128;
    canvas.height = 128;

    const tileSize = 4;

    for (let y = 0; y < canvas.width; y += tileSize)
    {
        for (let x = 0; x < canvas.height; x += tileSize)
        {
            const values = [];
            for (let n = 0; n < tileSize * tileSize; ++n)
            {
                values.push(256 * (n + 0.5) / (2 * tileSize * tileSize));
            }

            let i = values.length;
            while (--i)
            {
                const j = Math.floor((i + 1) * Math.random());
                [values[i], values[j]] = [values[j], values[i]];
            }

            i = 0;
            for (let y1 = 0; y1 < tileSize; ++y1)
            {
                for (let x1 = 0; x1 < tileSize; ++x1)
                {
                    let brightness = values[i++];
                    ctx.fillStyle = `rgb(${brightness},${brightness},${brightness})`;
                    ctx.fillRect(x + x1, y + y1, 1, 1);
                }
            }
        }
    }
}

main();