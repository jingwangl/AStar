using System;

public interface IHeapItem<T> : IComparable<T>
{
    int HeapIndex { get; set; }
}

public class Heap<T> where T : IHeapItem<T>
{
    T[] items;
    int count;
    public int Count => count;

    public Heap(int maxSize) => items = new T[maxSize];

    public void Add(T item)
    {
        item.HeapIndex = count;
        items[count] = item;
        SortUp(item);
        count++;
    }

    public T RemoveFirst()
    {
        T first = items[0];
        count--;
        items[0] = items[count];
        items[0].HeapIndex = 0;
        SortDown(items[0]);
        return first;
    }

    public void UpdateItem(T item) => SortUp(item);

    public bool Contains(T item) => Equals(items[item.HeapIndex], item);

    void SortDown(T item)
    {
        while (true)
        {
            int leftIndex = item.HeapIndex * 2 + 1;
            int rightIndex = item.HeapIndex * 2 + 2;
            int swapIndex = 0;

            if (leftIndex < count)
            {
                swapIndex = leftIndex;
                if (rightIndex < count && items[leftIndex].CompareTo(items[rightIndex]) < 0)
                    swapIndex = rightIndex;

                if (item.CompareTo(items[swapIndex]) < 0)
                    Swap(item, items[swapIndex]);
                else return;
            }
            else return;
        }
    }

    void SortUp(T item)
    {
        int parentIndex = (item.HeapIndex - 1) / 2;
        while (true)
        {
            T parentItem = items[parentIndex];
            if (item.CompareTo(parentItem) > 0)
            {
                Swap(item, parentItem);
            }
            else break;
            parentIndex = (item.HeapIndex - 1) / 2;
        }
    }

    void Swap(T a, T b)
    {
        items[a.HeapIndex] = b;
        items[b.HeapIndex] = a;
        int ai = a.HeapIndex;
        a.HeapIndex = b.HeapIndex;
        b.HeapIndex = ai;
    }
}
