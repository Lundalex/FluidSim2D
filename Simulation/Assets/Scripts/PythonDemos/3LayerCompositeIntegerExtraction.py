def get_last_chunk_key_ptype_porder(last_chunk_key_ptype_porder, chunk_num, ptypes_num):
    # Extract POrder
    p_order = last_chunk_key_ptype_porder // (chunk_num * ptypes_num)
    
    # Extract PType
    p_type = (last_chunk_key_ptype_porder % (chunk_num * ptypes_num)) // chunk_num
    
    # Extract LastChunkKey
    last_chunk_key = last_chunk_key_ptype_porder % chunk_num
    
    return last_chunk_key, p_type, p_order

# Example usage
chunk_num = 100  # Example value
ptypes_num = 5   # Example value
last_chunk_key_ptype_porder = 12345  # Example value

last_chunk_key, p_type, p_order = get_last_chunk_key_ptype_porder(last_chunk_key_ptype_porder, chunk_num, ptypes_num)
print(f"LastChunkKey: {last_chunk_key}, PType: {p_type}, POrder: {p_order}")