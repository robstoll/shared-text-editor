#Shared Text Editor

**Matthias Leitner s1310454019**

**Robert Stoll s1310454032**

##Description

The Shared Text Editor provides a simple collaborative text-editing tool, which allows multiple users to edit the same piece of text simultaneously. Changes to the edited document are distributed to all associated editors instantly.

The editor is implemented using C# .Net and the Windows Communication Foundation (WCF) for Peer-to-Peer as well as Client/Server communication.

##Architecture


###Communication

As previously mentioned the communication of the editor is implemented using WCF technologies. In order to fulfill the requirement for automatic document discovery between multiple editing clients we use NetPeerTcpBinding, which has been supported since .NET Framework 3.0. It provides everything we need in order to discover clients within the same LAN and broadcast requests for document discovery to all possible hosts. Once a client has started editing a document it communicates with the owner of the document via HTTP using WCF BasicHttpBinding. Clients are sending their patches to the owner and the owner in turn sends the applied patches via multicast to the other known editors as well as an acknowledgement to the client who send the corresponding update. Channeling the document update through the owner should help to avoid patching conflicts and avoid unnecessary broadcast messages if possible.

#### WCF Contracts

**P2P**

For discovering documents and clients the following contract is used:
```cs
        [OperationContract(IsOneWay = true)]
        void Connect(string member);

        [OperationContract(IsOneWay = true)]
        void Disconnect(string member);

        [OperationContract(IsOneWay = true)]
        void InitializeMesh();
        
        [OperationContract(IsOneWay = true)]
        void SynchronizeMemberList(string member);

        [OperationContract(IsOneWay = true)]
        void FindDocument(string host, string documentId, string memberName);
```
**Client/Server**

For the one-to-one communication between an editor and the owner of a document the following WCF contract is used:
```cs
    [ServiceContract(SessionMode = SessionMode.Allowed)]
    public interface ISharedTextEditorC2S
    {
        [OperationContract(IsOneWay = true)]
        void FindDocument(string host, string documentId, string memberName);

        [OperationContract(IsOneWay = true)]
        void UpdateRequest(UpdateDto dto);

        [OperationContract(IsOneWay = true)]
        void AckRequest(AcknowledgeDto dto);

         [OperationContract(IsOneWay = true)]
         void OpenDocument(DocumentDto dto);
    }
```

###Update/Sync Logic
The editors use the open source Diff, Match and patch libraries from Google Inc. which provide robust algorithms to perform the operations required for synchronizing plain text. These algorithmns implement the principles of Operational Transformation which also represents a core concept behind colloparative software by the company such as Google Docs and Google Wave. Creating patches reduces the amount of data transferred and allows the changes to be applied without relying on static indexes within the text.

###User Interface

The user interface is realized using the Windows Forms APIs. All the logic is decoupled from the UI and should therefore never block the user interaction.


##Dependencies

google-diff-match-patch

code.google.com/p/google-diff-match-patch

 

##Known issues
-   Open a document if there are multiple owners with the same document name. The owner responds to the document discovery response first is chosen.

##Time spent


Planing/Research: 8h

Implementation: 36h

Testing/Bugfixes: 10h


##User Guide

- Start the client 

- Click 'Connect' in order to join the P2P Mesh

- Wait until the connection to the mesh is established

- Enter the name of the document you want to edit

- If you want to join an existing document with the given name click 'Open'. If you want to create a new document click 'Create'

- To close an open tab, type "CTRL+W"

##Screenshots





