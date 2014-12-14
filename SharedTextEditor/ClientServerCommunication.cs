using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SharedTextEditor
{
    public interface IClientServerCommunication
    {
        void UpdateRequest(string host, UpdateDto dto);

        void AckRequest(string host, AcknowledgeDto dto);
        void OpenDocument(string host, DocumentDto documentDto);
    }

    class ClientServerCommunication : IClientServerCommunication
    {

        public void UpdateRequest(string host, UpdateDto updateDto)
        {
            using (var cf = GetChannelFactory(host))
            {
                cf.CreateChannel().UpdateRequest(updateDto);
            }
        }


        public void AckRequest(string host, AcknowledgeDto acknowledgeDto)
        {
            using (var cf = GetChannelFactory(host))
            {
                cf.CreateChannel().AckRequest(acknowledgeDto);
            }
        }

        public void OpenDocument(string host, DocumentDto documentDto)
        {
            using (var cf = GetChannelFactory(host))
            {
                cf.CreateChannel().OpenDocument(documentDto);
            }
        }

        private ChannelFactory<ISharedTextEditorC2S> GetChannelFactory(string host)
        {

            var binding = new BasicHttpBinding();
            var endpoint = new EndpointAddress(host);

            return new ChannelFactory<ISharedTextEditorC2S>(binding, endpoint);

        }
    }
}
