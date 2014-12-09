using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiffMatchPatch;

namespace SharedTextEditor
{
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }
            return left.SequenceEqual(right);
        }
        public int GetHashCode(byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            return key.Sum(b => b);
        }
    }

    public class Document
    {
        public string Id { get; set; }

        public string Owner { get; set; }

        public UpdateDto PendingUpdate { get; set; }

        public byte[] CurrentHash { get; set; }

        public string Content { get;set; }

        public string OwnerHost { get; set; }

        public UpdateDto OutOfSyncUpdate { get; set; }

        private readonly Dictionary<byte[], Revision> _revisionsByHash = new Dictionary<byte[], Revision>(new ByteArrayComparer());

        private readonly List<Revision> _revisionsByIndex = new List<Revision>();
        private readonly Dictionary<string, string> _editingHosts = new Dictionary<string, string>(); 

        public Revision GetRevision(byte[] hash)
        {
            return _revisionsByHash.ContainsKey(hash) ? _revisionsByHash[hash] : null;
        }

        public Revision GetRevision(int index)
        {
            if (_revisionsByIndex.Count > index)
            {
                return _revisionsByIndex[index];
            }
            return null;
        }

        public void AddRevision(Revision revision)
        {
            _revisionsByIndex.Add(revision);
            _revisionsByHash.Add(revision.UpdateDto.NewHash, revision);
        }

        public void AddEditor(string name, string host)
        {
             _editingHosts[name] = host;
        }

        public bool RemoveEditor(string name)
        {
            return _editingHosts.Remove(name);
        }

        public Dictionary<string, string> Editors()
        {
            return _editingHosts;
        }
    }

    public class Revision
    {
        public int Id { get; set; }

        public string Content { get; set; }

        public UpdateDto UpdateDto { get; set;}        
    }
}
