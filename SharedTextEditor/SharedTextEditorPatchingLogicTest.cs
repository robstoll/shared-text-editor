using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DiffMatchPatch;
using NUnit.Framework;
using Rhino.Mocks;

namespace SharedTextEditor
{
    [TestFixture]
    class SharedTextEditorPatchingLogicTest
    {
        //The following enconding is used where the order defines the order in which the patches are applied
        // X = first revision
        // Ax = Update x from member A
        // an example:
        //
        // UpdateRequestServer_ExistingIsXA1A2UpdateRequestXB1_PatchXA1B1A2
        //
        // stands for we have the initial state and A1 | A2 where already applied, 
        // update request is B1 based on X and resulting will be A1 | B1 | A2

        [Test]
        public void UpdateRequestServer_ExistingIsXUpdateRequestXA1_PatchXA1()
        {
            const string memberId = "1";
            const string documentId = "MyDoc";
            const string initialContent = "test";
            const string contentA1 = "tests";
            const string owner = "max";
            const string host = "http://localhost:9000";
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(initialContent));
            var diffMatchPath = new diff_match_patch();
            var editor = MockRepository.GenerateStub<SharedTextEditor>();
            editor.Stub(x => x.GetText(documentId)).Return(initialContent);
            var communication = MockRepository.GenerateStub<IClientServerCommunication>();

            //act
            var logic = new SharedTextEditorPatchingLogic(owner, host, editor, communication);

            editor.Raise(x => x.FindDocumentRequest += null, editor, documentId);
            logic.OpenDocument(new DocumentDto
            {
                DocumentId = documentId,
                Content = initialContent,
                Owner = owner,
                OwnerHost = host
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberId,
                PreviousHash = hash,
                Patch = diffMatchPath.patch_make(initialContent, contentA1)
            });


            //assert
            var args = editor.GetArgumentsForCallsMadeOn(x => x.UpdateText(null, null), x => x.IgnoreArguments());
            Assert.That(args[0][1], Is.EqualTo(initialContent));
            Assert.That(args[1][1], Is.EqualTo(contentA1));
        }

        [Test]
        public void UpdateRequestServer_ExistingIsXA1UpdateRequestXB1_PatchXA1B1()
        {
            const string memberIdA = "1";
            const string memberIdB = "2";
            const string documentId = "MyDoc";
            const string initialContent = "test";
            const string contentA1 = "tests";
            const string contentB1 = "testi";
            const string resultingContent = "testsi";
            const string owner = "max";
            const string host = "http://localhost:9000";
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(initialContent));
            var diffMatchPath = new diff_match_patch();
            var editor = MockRepository.GenerateStub<SharedTextEditor>();
            editor.Stub(x => x.GetText(documentId)).Return(initialContent).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1).Repeat.Once();
            var communication = MockRepository.GenerateStub<IClientServerCommunication>();

            //act
            var logic = new SharedTextEditorPatchingLogic(owner, host, editor, communication);

            editor.Raise(x => x.FindDocumentRequest += null, editor, documentId);
            logic.OpenDocument(new DocumentDto
            {
                DocumentId = documentId,
                Content = initialContent,
                Owner = owner,
                OwnerHost = host
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = hash,
                Patch = diffMatchPath.patch_make(initialContent, contentA1)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdB,
                PreviousHash = hash,
                Patch = diffMatchPath.patch_make(initialContent, contentB1)
            });


            //assert
            var args = editor.GetArgumentsForCallsMadeOn(x => x.UpdateText(null, null), x => x.IgnoreArguments());
            Assert.That(args[0][1], Is.EqualTo(initialContent));
            Assert.That(args[1][1], Is.EqualTo(contentA1));
            Assert.That(args[2][1], Is.EqualTo(resultingContent));
        }

        [Test]
        public void UpdateRequestServer_ExistingIsXB1UpdateRequestXA1_PatchXA1B1()
        {
            const string memberIdA = "1";
            const string memberIdB = "2";
            const string documentId = "MyDoc";
            const string initialContent = "test";
            const string contentA1 = "tests";
            const string contentB1 = "testi";
            const string resultingContent = "testsi";
            const string owner = "max";
            const string host = "net.tcp://localhost:9000";
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(initialContent));
            var diffMatchPath = new diff_match_patch();
            var editor = MockRepository.GenerateStub<SharedTextEditor>();
            editor.Stub(x => x.GetText(documentId)).Return(initialContent).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentB1).Repeat.Once();
            var communication = MockRepository.GenerateStub<IClientServerCommunication>();

            //act
            var logic = new SharedTextEditorPatchingLogic(owner, host, editor, communication);

            editor.Raise(x => x.FindDocumentRequest += null, editor, documentId);
            logic.OpenDocument(new DocumentDto
            {
                DocumentId = documentId,
                Content = initialContent,
                Owner = owner,
                OwnerHost = host
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdB,
                PreviousHash = hash,
                Patch = diffMatchPath.patch_make(initialContent, contentB1)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = hash,
                Patch = diffMatchPath.patch_make(initialContent, contentA1)
            });


            //assert
            var args = editor.GetArgumentsForCallsMadeOn(x => x.UpdateText(null, null), x => x.IgnoreArguments());
            Assert.That(args[0][1], Is.EqualTo(initialContent));
            Assert.That(args[1][1], Is.EqualTo(contentB1));
            Assert.That(args[2][1], Is.EqualTo(resultingContent));
        }

        [Test]
        public void UpdateRequestServer_ExistingIsXA1B1UpdateRequestA1A2_PatchXA1A2B1()
        {
            const string memberIdA = "1";
            const string memberIdB = "2";
            const string documentId = "MyDoc";
            const string initialContent = "test";
            const string contentA1 = "tests";
            const string contentA2 = "testst";
            const string contentB1 = "testi";
            const string contentA1B1 = "testsi";
            const string resultingContent = "teststi";
            const string owner = "max";
            const string host = "http://localhost:9000";
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] initialHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(initialContent));
            byte[] hashA1 = sha1.ComputeHash(Encoding.UTF8.GetBytes(contentA1));
            var diffMatchPath = new diff_match_patch();
            var editor = MockRepository.GenerateStub<SharedTextEditor>();
            editor.Stub(x => x.GetText(documentId)).Return(initialContent).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1B1).Repeat.Once();
            var communication = MockRepository.GenerateStub<IClientServerCommunication>();

            //act
            var logic = new SharedTextEditorPatchingLogic(owner, host, editor, communication);

            editor.Raise(x => x.FindDocumentRequest += null, editor, documentId);
            logic.OpenDocument(new DocumentDto
            {
                DocumentId = documentId,
                Content = initialContent,
                Owner = owner,
                OwnerHost = host
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = initialHash,
                Patch = diffMatchPath.patch_make(initialContent, contentA1)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdB,
                PreviousHash = initialHash,
                Patch = diffMatchPath.patch_make(initialContent, contentB1)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = hashA1,
                Patch = diffMatchPath.patch_make(contentA1, contentA2)
            });


            //assert
            var args = editor.GetArgumentsForCallsMadeOn(x => x.UpdateText(null, null), x => x.IgnoreArguments());
            Assert.That(args[0][1], Is.EqualTo(initialContent));
            Assert.That(args[1][1], Is.EqualTo(contentA1));
            Assert.That(args[2][1], Is.EqualTo(contentA1B1));
            Assert.That(args[3][1], Is.EqualTo(resultingContent));
        }

        [Test]
        public void UpdateRequestServer_ExistingIsXA1A2UpdateRequestXB1_PatchXA1B1A2()
        {
            const string memberIdA = "1";
            const string memberIdB = "2";
            const string documentId = "MyDoc";
            const string initialContent = "test";
            const string contentA1 = "tests";
            const string contentA2 = "testst";
            const string contentB1 = "testi";
            const string contentA1A2 = "testst";
            const string resultingContent = "testsit";
            const string owner = "max";
            const string host = "http://localhost:9000";
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] initialHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(initialContent));
            byte[] hashA1 = sha1.ComputeHash(Encoding.UTF8.GetBytes(contentA1));
            var diffMatchPath = new diff_match_patch();
            var editor = MockRepository.GenerateStub<SharedTextEditor>();
            editor.Stub(x => x.GetText(documentId)).Return(initialContent).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1A2).Repeat.Once();
            var communication = MockRepository.GenerateStub<IClientServerCommunication>();

            //act
            var logic = new SharedTextEditorPatchingLogic(owner, host, editor, communication);

            editor.Raise(x => x.FindDocumentRequest += null, editor, documentId);
            logic.OpenDocument(new DocumentDto
            {
                DocumentId = documentId,
                Content = initialContent,
                Owner = owner,
                OwnerHost = host
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = initialHash,
                Patch = diffMatchPath.patch_make(initialContent, contentA1)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = hashA1,
                Patch = diffMatchPath.patch_make(contentA1, contentA2)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdB,
                PreviousHash = initialHash,
                Patch = diffMatchPath.patch_make(initialContent, contentB1)
            });


            //assert
            var args = editor.GetArgumentsForCallsMadeOn(x => x.UpdateText(null, null), x => x.IgnoreArguments());
            Assert.That(args[0][1], Is.EqualTo(initialContent));
            Assert.That(args[1][1], Is.EqualTo(contentA1));
            Assert.That(args[2][1], Is.EqualTo(contentA1A2));
            Assert.That(args[3][1], Is.EqualTo(resultingContent));
        }

        [Test]
        public void UpdateRequestServer_ExistingIsXA1B1A2UpdateRequestXC1_PatchXA1B1A2()
        {
            const string memberIdA = "1";
            const string memberIdB = "2";
            const string memberIdC = "3";
            const string documentId = "MyDoc";
            const string initialContent = "test";
            const string contentA1 = "tests";
            const string contentA2 = "testst";
            const string contentB1 = "testi";
            const string contentC1 = "test ";
            const string contentA1B1 = "testsi";
            const string contentA1B1A2 = "teststi";
            const string resultingContent = "teststi ";
            const string owner = "max";
            const string host = "http://localhost:9000";
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] initialHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(initialContent));
            byte[] hashA1 = sha1.ComputeHash(Encoding.UTF8.GetBytes(contentA1));
            var diffMatchPath = new diff_match_patch();
            var editor = MockRepository.GenerateStub<SharedTextEditor>();
            editor.Stub(x => x.GetText(documentId)).Return(initialContent).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1B1).Repeat.Once();
            editor.Stub(x => x.GetText(documentId)).Return(contentA1B1A2).Repeat.Once();
            var communication = MockRepository.GenerateStub<IClientServerCommunication>();

            //act
            var logic = new SharedTextEditorPatchingLogic(owner, host, editor, communication);

            editor.Raise(x => x.FindDocumentRequest += null, editor, documentId);
            logic.OpenDocument(new DocumentDto
            {
                DocumentId = documentId,
                Content = initialContent,
                Owner = owner,
                OwnerHost = host
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = initialHash,
                Patch = diffMatchPath.patch_make(initialContent, contentA1)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdB,
                PreviousHash = initialHash,
                Patch = diffMatchPath.patch_make(initialContent, contentB1)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdA,
                PreviousHash = hashA1,
                Patch = diffMatchPath.patch_make(contentA1, contentA2)
            });

            logic.UpdateRequest(new UpdateDto
            {
                DocumentId = documentId,
                MemberName = memberIdC,
                PreviousHash = initialHash,
                Patch = diffMatchPath.patch_make(initialContent, contentC1)
            });


            //assert
            var args = editor.GetArgumentsForCallsMadeOn(x => x.UpdateText(null, null), x => x.IgnoreArguments());
            Assert.That(args[0][1], Is.EqualTo(initialContent));
            Assert.That(args[1][1], Is.EqualTo(contentA1));
            Assert.That(args[2][1], Is.EqualTo(contentA1B1));
            Assert.That(args[3][1], Is.EqualTo(contentA1B1A2));
            Assert.That(args[4][1], Is.EqualTo(resultingContent));
        }
    }
}
