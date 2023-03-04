﻿namespace O21.NE;

/// <remarks>https://jeffpar.github.io/kbarchive/kb/065/Q65122/</remarks>
public class NeFile
{
    private readonly Stream _input;
    private readonly ushort _segmentedHeaderOffset;
    private readonly ushort _resourceTableOffset;

    public NeFile(Stream input, ushort segmentedHeaderOffset, ushort resourceTableOffset)
    {
        _input = input;
        _segmentedHeaderOffset = segmentedHeaderOffset;
        _resourceTableOffset = resourceTableOffset;
    }

    public static NeFile ReadFrom(Stream input)
    {
        input.Seek(0x3CL, SeekOrigin.Begin);
        var segmentedHeaderOffset = input.ReadUInt16Le();
        input.Seek(segmentedHeaderOffset, SeekOrigin.Begin);

        Span<byte> signature = stackalloc byte[2];
        input.ReadExactly(signature);
        if (!signature.SequenceEqual("NE"u8))
            throw new Exception(
                $"""Invalid NE file signature: "{(char)signature[0]}{(char)signature[1]}" instead of "NE".""");

        // Read resource table offset at header + 0x24:
        input.Seek(segmentedHeaderOffset + 0x24, SeekOrigin.Begin);
        var resourceTableOffset = input.ReadUInt16Le();

        // Read the number of resource entries at header + 0x34:
        input.Seek(segmentedHeaderOffset + 0x34, SeekOrigin.Begin);
        var resourceEntryNumber = input.ReadUInt16Le();

        // Read resource alignment shift count at resource table offset:
        input.Seek(segmentedHeaderOffset + resourceTableOffset, SeekOrigin.Begin);
        var alignmentShiftCount = input.ReadUInt16Le();

        return new NeFile(input, segmentedHeaderOffset, resourceTableOffset);
    }

    public IEnumerable<NeResourceType> ReadResourceTable()
    {
        _input.Seek(_segmentedHeaderOffset + _resourceTableOffset + 2, SeekOrigin.Begin);
        while (true)
        {
            var typeId = _input.ReadUInt16Le();
            if ((typeId & (1 << 16)) != 0)
                throw new Exception($"Non-integer resource type id is not supported: {typeId}.");

            if (typeId == 0) yield break;

            var resourceCount = _input.ReadUInt16Le();
            _input.Seek(4, SeekOrigin.Current); // DD Reserved

            var resources = new NeResource[resourceCount];
            for (var r = 0; r < resourceCount; ++r)
            {
                resources[r] = ReadResource();
            }

            yield return new NeResourceType(typeId, resources);
        }
    }

    private NeResource ReadResource()
    {
        var offset = _input.ReadUInt16Le();
        var length = _input.ReadUInt16Le();

        // DW Flag word
        // DW Resource ID
        // DD Reserved
        _input.Seek(8, SeekOrigin.Current);

        return new NeResource(offset, length);
    }
}
