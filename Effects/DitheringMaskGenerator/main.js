function shuffle(arr) {
    for (let i = arr.length; i-- > 0;) {
        const j = Math.floor((i + 1) * Math.random())
        ;[arr[i], arr[j]] = [arr[j], arr[i]]
    }
}

function main() {
    const canvas = document.querySelector("#output")
    const ctx = canvas.getContext("2d")

    canvas.width = 128
    canvas.height = 128

    const tileSize = 4
    const numSteps = tileSize * tileSize

    for (let y = 0; y < canvas.width; y += tileSize) {
        for (let x = 0; x < canvas.height; x += tileSize) {
            const lower = []
            for (let n = 0; n < numSteps / 2; ++n) {
                lower.push((n + 0.5) / numSteps * 256)
            }

            const upper = []
            for (let n = numSteps / 2; n < numSteps; ++n) {
                upper.push((n + 0.5) / numSteps * 256)
            }

            shuffle(lower)
            shuffle(upper)

            const values = []
            for (let i = 0; i < lower.length; ++i) {
                // Checkerboard pattern
                if (Math.floor(2 * i / tileSize) % 2 == 0) {
                    values.push(lower[i])
                    values.push(upper[i])
                } else {
                    values.push(upper[i])
                    values.push(lower[i])
                }
            }

            let i = 0;
            for (let y1 = 0; y1 < tileSize; ++y1) {
                for (let x1 = 0; x1 < tileSize; ++x1) {
                    let brightness = values[i++]
                    ctx.fillStyle = `rgb(${brightness},${brightness},${brightness})`
                    ctx.fillRect(x + x1, y + y1, 1, 1)
                }
            }
        }
    }
}

main()