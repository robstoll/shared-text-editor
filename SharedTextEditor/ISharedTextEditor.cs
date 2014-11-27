﻿using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Runtime.Serialization;
using DiffMatchPatch;

namespace SharedTextEditor
{
    [ServiceContract(Namespace = "http://com.sharedtexteditor", CallbackContract = typeof(ISharedTextEditorP2P))]
    public interface ISharedTextEditorP2P
    {
        [OperationContract(IsOneWay = true)]
        void Connect(string member);

        [OperationContract(IsOneWay = true)]
        void Disconnect(string member);

        [OperationContract(IsOneWay = true)]
        void InitializeMesh();
        
        [OperationContract(IsOneWay = true)]
        void SynchronizeMemberList(string member);

        [OperationContract(IsOneWay = true)]
        void FindDocument(string documentId);

        [OperationContract(IsOneWay = true)]
        void DocumentDiscoveryResponse(DocumentDto document);

        [OperationContract(IsOneWay = true)]
        void UpdateRequest(UpdateDto dto);

        [OperationContract(IsOneWay = true)]
        void ForceReload(UpdateDto dto);

        [OperationContract(IsOneWay = true)]
        void AckRequest(AcknowledgeDto dto);
    }

    public interface ISharedTextEditorP2PChannel : ISharedTextEditorP2P, IClientChannel
    {
    }

     [ServiceContract(Namespace = "http://com.sharedtexteditor", CallbackContract = typeof(ISharedTextEditorP2P))]
    public interface ISharedTextEditorC2S
    {
        [OperationContract(IsOneWay = true)]
        void MemberHasDocument(string documentId, string memberName);

        [OperationContract(IsOneWay = true)]
        void DocumentRequest(string documentId);

        [OperationContract(IsOneWay = true)]
        void OpenDocument(DocumentDto document);

        [OperationContract(IsOneWay = true)]
        void UpdateRequest(UpdateDto dto);

        [OperationContract(IsOneWay = true)]
        void AckRequest(AcknowledgeDto dto);

        void FindDocument(string documentId);
    }

    [DataContract(Namespace = "http://com.sharedtexteditor")]
    public class DocumentDto
    {
        [DataMember]
        public string DocumentId { get; set; }

        [DataMember]
        public string Content { get; set; }
        [DataMember]
        public string Owner { get; set; }
        [DataMember]
        public int MyMemberId { get; set; }
    }

    [DataContract(Namespace = "http://com.sharedtexteditor")]
    public class AcknowledgeDto
    {
        [DataMember]
        public string DocumentId { get; set; }

        [DataMember]
        public byte[] PreviousHash { get; set; }

        [DataMember]
        public byte[] NewHash { get; set; }
    }


    [DataContract(Namespace = "http://com.sharedtexteditor")]
    [Serializable]
    public class UpdateDto
    {
        [DataMember]
        public int MemberId { get; set; }


        [DataMember]
        public string DocumentId { get; set; }

        [DataMember]
        public byte[] PreviousHash { get; set; }

        [DataMember]
        public byte[] NewHash { get; set; }

        [DataMember]
        public List<Patch> Patch { get; set; }
    }
}
