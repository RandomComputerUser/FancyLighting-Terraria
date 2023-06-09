import math
import sys

from numpy import float32


def gaussian_kernel(radius: int) -> list[float]:
    coefficients = [1]
    for _ in range(2 * radius):
        tmp = [1]
        for i in range(1, len(coefficients)):
            tmp.append(coefficients[i - 1] + coefficients[i])
        tmp.append(1)
        coefficients = tmp

    coefficient_sum = sum(coefficients)
    return [coefficients[i] / coefficient_sum for i in range(radius, -1, -1)]


def main() -> None:
    radius = int(sys.argv[1])
    kernel = [float32(x) for x in gaussian_kernel(radius)]

    print(f"Radius = {radius + 0.5}")
    print(f"{{ {', '.join(str(x) for x in kernel)} }}")


if __name__ == "__main__":
    main()
