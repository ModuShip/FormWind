using System;
using System.Diagnostics;
using System.Threading;

namespace FormWind;

/// <summary>Policy for errors while applying styles and painting: non-critical ones are swallowed and reported.</summary>
internal static class TwErrors
{
    /// <summary>Exceptions after which the process state is unreliable and that must not be caught.</summary>
    public static bool IsCritical(Exception exception) =>
        exception is OutOfMemoryException
        || exception is ThreadAbortException
        || exception is AccessViolationException
        || exception is StackOverflowException;

    /// <summary>Writes an error to the Debug output where the provider is unknown (painting).</summary>
    public static void Trace(string where, Exception exception) =>
        Debug.WriteLine($"FormWind: error during {where}: {exception}");
}

/// <summary>Data of the <see cref="TailwindProvider.StyleError"/> event.</summary>
public sealed class TwStyleErrorEventArgs : EventArgs
{
    public TwStyleErrorEventArgs(object target, string classes, Exception exception)
    {
        Target = target;
        Classes = classes;
        Exception = exception;
    }

    /// <summary>The styled object: a <see cref="System.Windows.Forms.Control"/> or a <see cref="System.Windows.Forms.DataGridViewColumn"/>.</summary>
    public object Target { get; }

    /// <summary>The classes that were being applied.</summary>
    public string Classes { get; }

    public Exception Exception { get; }
}
