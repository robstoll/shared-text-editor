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

        public int MyMemberId { get; set; }

        public UpdateDto PendingUpdate { get; set; }

        public byte[] CurrentHash { get; set; }

        public string Content { get;set; }

        public UpdateDto OutOfSyncUpdate { get; set; }

        private readonly Dictionary<byte[], Revision> _revisionsByHash = new Dictionary<byte[], Revision>(new ByteArrayComparer());

        private readonly List<Revision> _revisionsByIndex = new List<Revision>();

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
    }

    public class Revision
    {
        public int Id { get; set; }

        public string Content { get; set; }

        public UpdateDto UpdateDto { get; set;}        
    }
}
