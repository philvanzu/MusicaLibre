using System.Collections.Generic;
using System.Linq;

namespace MusicaLibre.Services;

public class CompositeComparer<T> : IComparer<T>
{
    private readonly List<IComparer<T>> _comparers;

    public CompositeComparer(IEnumerable<IComparer<T>> comparers)
        => _comparers = comparers.ToList();

    public int Compare(T? x, T? y)
    {
        foreach (var cmp in _comparers)
        {
            int result = cmp.Compare(x, y);
            if (result != 0)
                return result;
        }
        return 0;
    }
}