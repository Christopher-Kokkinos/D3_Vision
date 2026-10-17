using System.Reflection.PortableExecutable;
using System.Text;
using D3Vision.Infrastructure;

internal static class MapCacheDiagnostics
{
    public static void Print(ReadOnlyProcess memory, nint moduleBase, string executable)
    {
        using var stream = File.OpenRead(executable);
        using var pe = new PEReader(stream);
        var section = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".data");
        if (section.VirtualSize is <= 0 or > 8 * 1024 * 1024)
            throw new InvalidOperationException(
                "Diagnostic data section bounds failed validation."
            );
        var data = new byte[section.VirtualSize];
        for (int offset = 0; offset < data.Length; offset += 0x100000)
        {
            int count = Math.Min(0x100000, data.Length - offset);
            memory.Read(moduleBase + section.VirtualAddress + offset, count).CopyTo(data, offset);
        }
        byte[] name = Encoding.ASCII.GetBytes(
            "Root.NormalLayer.minimap_dialog_backgroundScreen.minimap_dialog_pve\0"
        );
        var area = memory.Pointer(moduleBase + 0x1672A68);
        var areaHeader = memory.Read(area, 0xB0);
        Console.WriteLine($"Area header 0x{area:X}");
        for (int i = 0; i < areaHeader.Length; i += 8)
            Console.WriteLine($"  +0x{i:X}: 0x{BitConverter.ToUInt64(areaHeader, i):X16}");
        var first = memory.Read((nint)BitConverter.ToInt64(areaHeader, 0x20), 88);
        Console.WriteLine(
            $"First cache record SNO {BitConverter.ToInt32(first)}, scene {BitConverter.ToInt32(first, 4)}, world {BitConverter.ToInt32(first, 8)}"
        );
        Console.WriteLine(
            $"Cache links 0x{BitConverter.ToUInt64(first, 72):X}, 0x{BitConverter.ToUInt64(first, 80):X}"
        );
        foreach (
            var token in new[]
            {
                Encoding.ASCII.GetBytes("Horazon"),
                Encoding.Unicode.GetBytes("Horazon"),
            }
        )
        {
            int index = data.AsSpan().IndexOf(token);
            if (index >= 0)
            {
                Console.WriteLine($"Area name candidate RVA 0x{section.VirtualAddress + index:X}");
                for (int word = Math.Max(0, index - 32); word < index; word += 8)
                    Console.WriteLine(
                        $"  RVA 0x{section.VirtualAddress + word:X}: 0x{BitConverter.ToUInt64(data, word):X16}"
                    );
            }
        }
        for (int offset = 0; offset < data.Length; )
        {
            int found = data.AsSpan(offset).IndexOf(name);
            if (found < 0)
                break;
            int index = offset + found;
            int reference = index - 8;
            Console.WriteLine($"Minimap UI reference RVA 0x{section.VirtualAddress + reference:X}");
            for (int word = Math.Max(0, reference - 128); word < reference; word += 8)
                Console.WriteLine(
                    $"  RVA 0x{section.VirtualAddress + word:X}: 0x{BitConverter.ToUInt64(data, word):X16}"
                );
            offset = index + name.Length;
        }
    }
}
