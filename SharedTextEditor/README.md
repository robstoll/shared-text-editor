#Shared Text Editor

**Matthias Leitner s1310454019**
**Robert Stoll s1310454032**

##Description

The Shared Text Editor provides a simple collaborative text-editing tool, which allows multiple users to edit the same piece of text simultaneously.  Changes to the edited document are distributed to all associated editors instantly.

The editor is implemented using C# .Net and the Windows Communication Foundation (WCF) for Peer-to-Peer as well as Client/Server communication.

##Architecture

###Communication

As previously mentioned the communication of the editor is implemented using WCF technologies. In order to fulfill the requirement for automatic document discovery between multiple editing clients we use NetPeerTcpBinding, which has been supported since .NET Framework 3.0. It provides everything we need in order to discover clients within the same LAN and broadcast requests for document discovery to all possible hosts. Once a client has started editing a document it communicates with the owner of the document via HTTP using WCF BasicHttpBinding, the owner of the document will then multicast the patch of changes to all other known editors.  Channeling the document update trough the owner should help to avoid patching conflicts and avoid unnecessary broadcast messages if possible.


###Update Logic
The editors uses the open source Diff, Match and patch libraries from Google Inc. which provide robust algorithms to perform the operations required for synchronizing plain text.

###User Interface

The user interface is realized using the Windows Forms APIs.


##Dependencies

google-diff-match-patch

code.google.com/p/google-diff-match-patch

 

##Known issues
##Time spent
##User Guide

##Screenshots





