def bitonic_sort(arr, low, count, dir):
    if count > 1:
        k = count // 2
        bitonic_sort(arr, low, k, 1)
        bitonic_sort(arr, low + k, k, 0)
        bitonic_merge(arr, low, count, dir)

def bitonic_merge(arr, low, count, dir):
    if count > 1:
        k = count // 2
        for i in range(low, low + k):
            if (arr[i] > arr[i + k] and dir == 1) or (arr[i] < arr[i + k] and dir == 0):
                arr[i], arr[i + k] = arr[i + k], arr[i]
        bitonic_merge(arr, low, k, dir)
        bitonic_merge(arr, low + k, k)

def sort_bitonic(arr):
    bitonic_sort(arr, 0, len(arr))

# Example usage:
arr = [3, 7, 4, 8, 6, 2, 1, 5, 15, 14, 13, 12, 11, 10, 9, 0, 3, 7, 4, 8, 6, 2, 1, 5, 15, 14, 13, 12, 11, 10, 9, 0]
sort_bitonic(arr)
print(arr)