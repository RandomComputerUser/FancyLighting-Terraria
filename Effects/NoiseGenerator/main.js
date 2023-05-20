function shuffle(arr) {
    for (let i = arr.length; i-- > 0;) {
        const j = Math.floor((i + 1) * Math.random())
        ;[arr[i], arr[j]] = [arr[j], arr[i]]
    }
}

function main() {
    const canvas = document.querySelector("#output")
    const ctx = canvas.getContext("2d")

    canvas.width = 256
    canvas.height = 256
    const size = canvas.width * canvas.height

    const noise = []
    for (let i = 0; i < size; ++i) {
        noise.push(Math.floor(i / size * 256))
    }
    shuffle(noise)

    let i = 0
    for (let y = 0; y < canvas.width; ++y) {
        for (let x = 0; x < canvas.height; ++x) {
            let brightness = noise[i++]
            ctx.fillStyle = `rgb(${brightness},${brightness},${brightness})`
            ctx.fillRect(x, y, 1, 1)
        }
    }
}

main()