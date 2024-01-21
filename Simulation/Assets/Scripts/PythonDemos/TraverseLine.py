from math import ceil, floor

# Doesn't work in certain cases. Hlsl version always works though

# List to simulate AppendBuffer
ChunkAppendBuffer = []

def TraverseLine(startX, startY, endX, endY):
    k = (endY-startY) / (endX-startX)
    print(k)
    searchHeight = ceil(k) + 1
    xBase = floor(startX)
    yBase = floor(startY)
    for y in range(yBase, yBase+searchHeight):
        ChunkAppendBuffer.append((xBase, y))
    
    dx = ceil(startX) - startX
    yBase += dx * k
    yBaseUse = floor(yBase)
    xBase += 1

    for y in range(yBaseUse, yBaseUse+searchHeight):
        ChunkAppendBuffer.append((xBase, y))
    
    while (xBase < endX-1):
        xBase += 1
        yBase += k
        yBaseUse = floor(yBase)
        for y in range(yBaseUse, yBaseUse+searchHeight):
            ChunkAppendBuffer.append((xBase, y))
    
    return ChunkAppendBuffer

MaxInfluenceRadius = 1
print(TraverseLine(0.5/MaxInfluenceRadius,
                   1.5/MaxInfluenceRadius,
                   6.5/MaxInfluenceRadius,
                   9.5/MaxInfluenceRadius))