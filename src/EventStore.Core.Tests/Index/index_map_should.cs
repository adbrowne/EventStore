using System;
using System.IO;
using System.Linq;
using EventStore.Core.Exceptions;
using EventStore.Core.Index;
using EventStore.Core.Util;
using NUnit.Framework;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class index_map_should: SpecificationWithDirectory
    {
        private string _indexMapFileName;
        private string _ptableFileName;
        private IndexMap _emptyIndexMap;
        private PTable _ptable;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            _indexMapFileName = GetFilePathFor("index.map");
            _ptableFileName = GetFilePathFor("ptable");

            _emptyIndexMap = IndexMap.FromFile(_indexMapFileName);

            var memTable = new HashListMemTable(maxSize: 10);
            memTable.Add(0, 1, 2);
            _ptable = PTable.FromMemtable(memTable, _ptableFileName);
        }

        [TearDown]
        public override void TearDown()
        {
            _ptable.MarkForDestruction();
            base.TearDown();
        }

        [Test]
        public void not_allow_negative_prepare_checkpoint_when_adding_ptable()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _emptyIndexMap.AddPTable(_ptable, -1, 0, _ => true, new GuidFilenameProvider(PathName)));
        }

        [Test]
        public void not_allow_negative_commit_checkpoint_when_adding_ptable()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _emptyIndexMap.AddPTable(_ptable, 0, -1, _ => true, new GuidFilenameProvider(PathName)));
        }

        [Test]
        public void throw_corruptedindexexception_when_prepare_checkpoint_is_less_than_minus_one()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, -2, 0, null);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, 2));
        }

        [Test]
        public void allow_prepare_checkpoint_equal_to_minus_one_if_no_ptables_are_in_index()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, -1, 0, null);
            Assert.DoesNotThrow(() =>
            {
                var indexMap = IndexMap.FromFile(_indexMapFileName, 2);
                indexMap.InOrder().ToList().ForEach(x => x.Dispose());
            });
        }

        [Test]
        public void throw_corruptedindexexception_if_prepare_checkpoint_is_minus_one_and_there_are_ptables_in_indexmap()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, -1, 0, _ptableFileName);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, 2));
        }

        [Test]
        public void throw_corruptedindexexception_when_commit_checkpoint_is_less_than_minus_one()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, 0, -2, null);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, 2));
        }

        [Test]
        public void allow_commit_checkpoint_equal_to_minus_one_if_no_ptables_are_in_index()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, 0, -1, null);
            Assert.DoesNotThrow(() =>
            {
                var indexMap = IndexMap.FromFile(_indexMapFileName, 2);
                indexMap.InOrder().ToList().ForEach(x => x.Dispose());
            });
        }

        [Test]
        public void throw_corruptedindexexception_if_commit_checkpoint_is_minus_one_and_there_are_ptables_in_indexmap()
        {
            CreateArtificialIndexMapFile(_indexMapFileName, 0, -1, _ptableFileName);
            Assert.Throws<CorruptIndexException>(() => IndexMap.FromFile(_indexMapFileName, 2));
        }

        private void CreateArtificialIndexMapFile(string filePath, long prepareCheckpoint, long commitCheckpoint, string ptablePath)
        {
            using (var memStream = new MemoryStream())
            using (var memWriter = new StreamWriter(memStream))
            {
                memWriter.WriteLine(new string('0', 32)); // pre-allocate space for MD5 hash
                memWriter.WriteLine("1");
                memWriter.WriteLine("{0}/{1}", prepareCheckpoint, commitCheckpoint);
                if (!string.IsNullOrWhiteSpace(ptablePath))
                {
                    memWriter.WriteLine("0,0,{0}", ptablePath);
                }
                memWriter.Flush();

                using (var f = File.OpenWrite(filePath))
                using (var fileWriter = new StreamWriter(f))
                {
                    memStream.Position = 0;
                    memStream.CopyTo(f);

                    memStream.Position = 32;
                    var hash = MD5Hash.GetHashFor(memStream);
                    f.Position = 0;
                    for (int i = 0; i < hash.Length; ++i)
                    {
                        fileWriter.Write(hash[i].ToString("X2"));
                    }
                    fileWriter.WriteLine();
                    fileWriter.Flush();
                    f.FlushToDisk();
                }
            }
        }
    }
}