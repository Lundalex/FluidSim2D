from math import log2
from math import ceil
from math import pow

# https://en.wikipedia.org/wiki/Bitonic_sorter

def SortPair(a, b):
    if a > b:
        high = a
        low = b
    else:
        high = b
        low = a
    return low, high

def StartSort(nums):
    depth = ceil(log2(len(nums)))
    
    return BitonicSort(depth, nums)

# This will be the equivelant to a BLUE box from the alternative visual representation of the bitonic mergesort algorithm
def BitonicSort(depth, nums):
    if depth > 1:
        len = int(pow(2, depth))
        sortedTop = BitonicSort(depth-1, nums[0:int(len/2)])      # Pass on to inner top sort
        sortedBottom = BitonicSort(depth-1, nums[int(len/2):len]) # Pass on to inner bottom sort
        return BitonicMerge(sortedBottom, sortedTop)
    
    # depth == 0 -> We will now sort the most inner pairs
    return SortPair(nums[0], nums[1])

def BitonicMerge(numsA, numsB):
    
    
    pass

# print(Sort4([9,3,5,3]))
print(StartSort([9,3,5,7]))