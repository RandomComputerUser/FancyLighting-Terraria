import math
import sys

from numpy import float32


def hemisphere_kernel(radius: int) -> list[float]:
    radius_squared = (radius + 0.5) ** 2
    kernel = [math.sqrt(radius_squared - x ** 2) for x in range(-radius, radius + 1)]
    kernel_sum = math.fsum(kernel)
    return [kernel[i] / kernel_sum for i in range(radius, -1, -1)]


def main() -> None:
    radius = int(sys.argv[1])
    kernel = [float32(x) for x in hemisphere_kernel(radius)]

    print(f"Radius = {radius + 0.5}")
    print(f"{{ {', '.join(str(x) for x in kernel)} }}")


if __name__ == "__main__":
    main()
