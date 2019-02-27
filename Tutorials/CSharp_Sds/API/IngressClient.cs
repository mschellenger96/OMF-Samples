﻿//Copyright 2018 OSIsoft, LLC
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//<http://www.apache.org/licenses/LICENSE-2.0>
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IngressServiceAPI.API
{
    /// <summary>
    /// Client used to send OMF message to the ingress service.
    /// </summary>
    public class IngressClient : IDisposable
    {
        public const string CurrentOMFVersion = "1.0";
        private readonly HttpClient _httpClient;
        private readonly string _omfEndpoint;

        public bool UseCompression { get; set; }

        /// <summary>
        /// Create an IngressClient by passing it the required connection information.
        /// </summary>
        /// <param name="serviceUrl">The HTTP endpoint for the ingress service.</param>
        /// <param name="producerToken">Security token used to authenticate with the service.</param>     
        public IngressClient(string serviceUrl, string tenantId, string namesapceId, string clientId, string clientSecret)
        {
            AuthenticationHandler authenticationHandler = new AuthenticationHandler(new Uri(serviceUrl), clientId, clientSecret);
            _httpClient = new HttpClient(authenticationHandler)
            {
                BaseAddress = new Uri(serviceUrl)
            };
            _omfEndpoint = $"api/tenants/{tenantId}/namespaces/{namesapceId}/omf2";    
        }

        /// <summary>
        /// Sends a collection of JSONSchema strings to ingress service.  The JSONSchema describes
        /// the types used for the data that will be sent.
        /// </summary>
        /// <param name="types">A collection of JSONSchema string.</param>
        public void CreateTypes(IEnumerable<string> types)
        {
            string json = string.Format("[{0}]", string.Join(",", types));
            var bytes = Encoding.UTF8.GetBytes(json);
            SendMessageAsync(bytes, MessageType.Type, MessageAction.Create).Wait();
        }

        /// <summary>
        /// Sends a collection of ContainerId, TypeId pairs to the endpoint.
        /// </summary>
        /// <param name="streams"></param>
        public void CreateContainers(IEnumerable<ContainerInfo> streams)
        {
            string json = JsonConvert.SerializeObject(streams);
            var bytes = Encoding.UTF8.GetBytes(json);
            SendMessageAsync(bytes, MessageType.Container, MessageAction.Create).Wait();
        }

        /// <summary>
        /// Sends the actual values to the ingress service.  This is async to allow for higher
        /// throughput to the event hub.
        /// </summary>
        /// <param name="values">A collection of values and their associated streams.</param>
        /// <returns></returns>
        public Task SendValuesAsync(IEnumerable<StreamValues> values)
        {
            string json = JsonConvert.SerializeObject(values);
            var bytes = Encoding.UTF8.GetBytes(json);
            return SendMessageAsync(bytes, MessageType.Data, MessageAction.Create);
        }

        private async Task SendMessageAsync(byte[] body, MessageType msgType, MessageAction action)
        {
            Message msg = new Message();
            msg.MessageType = msgType;
            msg.Action = action;
            msg.MessageFormat = MessageFormat.JSON;
            msg.Body = body;
            msg.Version = CurrentOMFVersion;

            if (UseCompression)
                msg.Compress(MessageCompression.GZip);

            HttpContent content = HttpContentFromMessage(msg);
            HttpResponseMessage response = await _httpClient.PostAsync(_omfEndpoint, content);
            string json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
        }

        private HttpContent HttpContentFromMessage(Message msg)
        {
            ByteArrayContent content = new ByteArrayContent(msg.Body);
            foreach(var header in msg.Headers)
            {
                content.Headers.Add(header.Key, header.Value);
            }
            return content;
        }


        #region IDisposable
        private bool _disposed = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
