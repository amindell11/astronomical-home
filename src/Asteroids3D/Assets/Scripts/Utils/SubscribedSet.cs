using System.Collections.Generic;
using System.Linq;

namespace Utils
{
    public class SubscribedSet<T> : HashSet<T> where T : class
    {
        public System.Action<T> OnAdd;
        public System.Action<T> OnRemove;

        public SubscribedSet(System.Action<T> add = null, System.Action<T> remove = null)
        {
            OnAdd = add;
            OnRemove = remove;
        }

        public new bool Add(T item)
        {
            if (item is null || !base.Add(item)) return false;
            OnAdd?.Invoke(item);
            return true;
        }

        private new bool Remove(T item)
        {
            if (item == null) return false;
            if (!base.Remove(item)) return false;
            OnRemove?.Invoke(item);
            return true;
        }

        public new void Clear()
        {
            if (OnRemove != null)
                foreach (var item in this.Where(item => item != null))
                    OnRemove(item);
            base.Clear();
        }

        public new int RemoveWhere(System.Predicate<T> match)
        {
            var removedItems = this.Where(item => match(item)).ToList();
            return removedItems.Count(Remove);
        }
        public void Consolidate()
        {
            RemoveWhere(item => item == null);
        }

        public void AddAll(IEnumerable<T> items)
        {
            foreach (var i in items)
                if (i != null) Add(i);
        }
        
    }
}