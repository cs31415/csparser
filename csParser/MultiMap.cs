using System.Collections.Generic;

namespace csParser
{
    /// <summary>
    /// MultiMap is a dictionary that allows multiple entries per key
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class MultiMap<K, V>
    {
        #region Private data
        Dictionary<K, List<V>> m_Map;
        #endregion

        #region Public methods
        public MultiMap()
        {
            m_Map = new Dictionary<K, List<V>>();
        }
        public void Clear()
        {
            m_Map.Clear();
        }
        public void Add(K key, V val)
        {
            List<V> vals;
            if (m_Map.TryGetValue(key, out vals))
            {
                vals.Add(val);
            }
            else
            {
                vals = new List<V>();
                vals.Add(val);
                m_Map[key] = vals;
            }
        }
        public List<V> this[K key]
        {
            get
            {
                List<V> vals;
                m_Map.TryGetValue(key, out vals);
                return vals;
            }
        }
        public IEnumerable<K> Keys
        {
            get
            {
                return m_Map.Keys;
            }
        }
        public bool ContainsKey(K key)
        {
            return m_Map.ContainsKey(key);
        }
        public bool Contains(K key, V val)
        {
            List<V> vals;
            if (m_Map.TryGetValue(key, out vals))
            {
                if (vals.Contains(val))
                    return true;
            }
            return false;
        }
        #endregion
    }
}
