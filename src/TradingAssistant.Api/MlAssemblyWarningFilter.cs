using System.Text;

/// <summary>
/// TextWriter wrapper that filters out non-fatal ML.NET assembly reference warnings
/// written by JasperFx.RuntimeCompiler.AssemblyGenerator via Console.WriteLine.
/// Microsoft.ML.OneDal is a native Intel library (no managed assembly).
/// Microsoft.ML.FastTree is a transitive dependency not needed for code generation.
/// </summary>
internal sealed class MlAssemblyWarningFilter : TextWriter
{
    private readonly TextWriter _inner;

    public MlAssemblyWarningFilter(TextWriter inner) => _inner = inner;

    public override Encoding Encoding => _inner.Encoding;

    public override void WriteLine(string? value)
    {
        if (value is not null && IsMlAssemblyWarning(value))
            return;

        _inner.WriteLine(value);
    }

    public override void Write(string? value)
    {
        if (value is not null && IsMlAssemblyWarning(value))
            return;

        _inner.Write(value);
    }

    private static bool IsMlAssemblyWarning(string text) =>
        text.Contains("Microsoft.ML.OneDal") ||
        text.Contains("Microsoft.ML.FastTree") ||
        text.Contains("Could not make an assembly reference to Microsoft.ML");

    public override void Flush() => _inner.Flush();
    protected override void Dispose(bool disposing) { if (disposing) _inner.Flush(); }
}
