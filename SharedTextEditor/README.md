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

The following figure depicts a few use case scenarios and is used as basis to explain how operational transformation was implemented:
![(use case](https://github.com/matthiasleitner/shared-text-editor/blob/improvements/SharedTextEditor/Operational%20Transformation%20-%20Problem.png)

User A is the owner of the document in the above scenario. That means he has created it using the "Create" Button. User B and C both open the document (with the "Open" Button) - depicted by the <get doc> in the above figure.

User A modifies the document and patch a2 is generated. The document has now revision A2 and the owner sends the patch a2 to the remaining editors (denoted by the two green lines going out of A2).

As a next step, User A, B and C simultaneously modify the document. User A creates the patch a3 and sends it to the editors. That is straight forward, an owner has always precedence over other simultaneous modifications. 

Let's see what happens to the modification of user B. User B creates the patch b3 and sends an UpdateRequest (denoted by an orange line) to the owner (User A). Now, the owner checks whether the update is based on the current state or not. This is depicted by b3 > A2 (which shall be read as b3 is based on A2). A2 is not the current state and thus the owner checks whether it is not further behind than 5 commits of the current state (this number is configurable). This is depicted by A3 < A2 + 5 (which shall be read as is A3 not newer than A2 + 5 commits). In this case A2 is not older than 5 commits. As next step, the owner verifies if the patch put on A2 has precedence over the given path b3. The patch put on A2 was a2 and a2 has precedence over b3 (depicted by b3 > a2 which shall be read as b3 has to follow after a2). In this case, the server can put the patch b3 on top of A3 which creates the new state A4. An update with the patch b3 is send to user C and an acknowledge (denoted by a blue line) is sent to user B. 

As one surely noticed, there is a slight problem in the whole story. User B received the patch a3 before the acknowledge for his UpdateRequest b3. In this case the user proceeds as follows. First it checks whether the given update is based on the last state. In this case this is true, thus user B checks whether the given patch a3 has precedence over the pending update request b3. This is true as well and thus user B applies the patch a3 on top of A2 and enter the state A3 as well. The patch b3 is recalculated and is now based on A3. Once user B receives the acknowledge for b3 it checks whether the...  

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





