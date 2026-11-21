using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

public static partial class TwGrid
{
    /// <summary>
    /// Locks whole rows (row rights) with one check: in a locked row every Tw input column (<see cref="TwTextColumn"/>,
    /// <see cref="TwSelectColumn"/>) is drawn as plain text, the list does not open, and editing any cell is cancelled
    /// (FormWind cancels <c>CellBeginEdit</c>). Combines with <see cref="TwSelectColumn.LockedFor"/>: a row is locked when
    /// either says so. The check gets only rows the grid and its data source agree on. <c>null</c> removes the lock.
    /// <code>
    /// TwGrid.SetRowLock(grid, row => !CanEdit(grid.Rows[row]));
    /// </code>
    /// </summary>
    public static void SetRowLock(DataGridView grid, Func<int, bool>? locked)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        TwRowLock.Set(grid, locked);
    }

    /// <summary>The check given to <see cref="SetRowLock"/> (<c>null</c> = none).</summary>
    public static Func<int, bool>? GetRowLock(DataGridView grid)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        return TwRowLock.Get(grid);
    }
}

/// <summary>The row lock of a grid (<see cref="TwGrid.SetRowLock"/>): the check and the <c>CellBeginEdit</c> handler.</summary>
internal sealed class TwRowLock
{
    private static readonly ConditionalWeakTable<DataGridView, TwRowLock> s_locks = new();
    private Func<int, bool> _locked;

    private TwRowLock(Func<int, bool> locked) => _locked = locked;

    public static void Set(DataGridView grid, Func<int, bool>? locked)
    {
        if (s_locks.TryGetValue(grid, out var existing))
        {
            if (locked is null)
            {
                grid.CellBeginEdit -= OnCellBeginEdit;
                s_locks.Remove(grid);
            }
            else existing._locked = locked;
        }
        else if (locked is not null)
        {
            s_locks.Add(grid, new TwRowLock(locked));
            grid.CellBeginEdit += OnCellBeginEdit;
        }
        // Kilit çizimi değiştirir (düz metin / giriş kutusu); açık bir düzenleme kilitli satırda kalmasın.
        if (locked is not null && grid.IsCurrentCellInEditMode && IsLocked(grid, grid.CurrentCellAddress.Y))
        {
            grid.CancelEdit();
            grid.EndEdit();
        }
        grid.Invalidate();
    }

    public static Func<int, bool>? Get(DataGridView grid) => s_locks.TryGetValue(grid, out var rowLock) ? rowLock._locked : null;

    // Yalnızca ızgara ile veri kaynağının uzlaştığı satırlar sorulur (bkz. TwSelectColumn.ItemsAt).
    public static bool IsLocked(DataGridView? grid, int rowIndex) =>
        grid is not null && s_locks.TryGetValue(grid, out var rowLock) && TwInputCell.HasRow(grid, rowIndex) && rowLock._locked(rowIndex);

    private static void OnCellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (!e.Cancel && IsLocked(sender as DataGridView, e.RowIndex)) e.Cancel = true;
    }
}
