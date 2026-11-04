// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Twilio;
using Twilio.Clients;
using Twilio.Http;
using Twilio.Rest.Api.V2010.Account;

namespace Rock.Tests.Communication
{
    /// <summary>
    /// Tests that the Twilio transport sends from a WhatsApp system phone number over WhatsApp, and leaves SMS alone.
    /// </summary>
    [TestClass]
    public class TwilioWhatsAppTests
    {
        private const string Recipient = "+16235550100";

        [TestCleanup]
        public void Cleanup()
        {
            TwilioClient.Invalidate();
        }

        [TestMethod]
        public async Task SendFromWhatsAppNumber_AddsWhatsAppChannelToBothAddresses()
        {
            // A system phone number holds at most 20 characters, so a WhatsApp sender is stored without its "+".
            var request = await SendThroughTransportAsync( "whatsapp:14155238886", Recipient );

            Assert.AreEqual( "whatsapp:+14155238886", GetPostParam( request, "From" ) );
            Assert.AreEqual( "whatsapp:+16235550100", GetPostParam( request, "To" ) );
        }

        [TestMethod]
        public async Task SendFromWhatsAppNumber_AlreadyInWhatsAppForm_IsNotPrefixedTwice()
        {
            var request = await SendThroughTransportAsync( "whatsapp:+14155238886", "whatsapp:+16235550100" );

            Assert.AreEqual( "whatsapp:+14155238886", GetPostParam( request, "From" ) );
            Assert.AreEqual( "whatsapp:+16235550100", GetPostParam( request, "To" ) );
        }

        [TestMethod]
        public async Task SendFromSmsNumber_IsUnchanged()
        {
            var request = await SendThroughTransportAsync( "+16235550199", Recipient );

            Assert.AreEqual( "+16235550199", GetPostParam( request, "From" ) );
            Assert.AreEqual( Recipient, GetPostParam( request, "To" ) );
        }

        /// <summary>
        /// Sends one message through the transport's private send method, with a fake Twilio client that records the
        /// request instead of calling Twilio.
        /// </summary>
        private static async Task<Request> SendThroughTransportAsync( string fromPhone, string toPhone )
        {
            var fakeClient = new RecordingTwilioRestClient();
            TwilioClient.SetRestClient( fakeClient );

            var sendMethod = typeof( Rock.Communication.Transport.Twilio ).GetMethod( "SendTwilioMessageAsync", BindingFlags.NonPublic | BindingFlags.Static );
            Assert.IsNotNull( sendMethod, "Twilio.SendTwilioMessageAsync not found." );

            var sendTask = ( Task<MessageResource> ) sendMethod.Invoke( null, new object[] { fromPhone, null, null, toPhone, "Hello" } );
            await sendTask;

            Assert.IsNotNull( fakeClient.LastRequest, "No request reached the Twilio client." );
            return fakeClient.LastRequest;
        }

        private static string GetPostParam( Request request, string key )
        {
            return request.PostParams.Single( p => p.Key == key ).Value;
        }

        /// <summary>
        /// Records the request and answers with a queued message.
        /// </summary>
        private sealed class RecordingTwilioRestClient : ITwilioRestClient
        {
            public Request LastRequest { get; private set; }

            public string AccountSid => "AC00000000000000000000000000000000";

            public string Region => null;

            public HttpClient HttpClient => null;

            public Response Request( Request request )
            {
                LastRequest = request;
                return new Response( HttpStatusCode.Created, "{\"sid\":\"SM00000000000000000000000000000000\",\"status\":\"queued\"}", null );
            }

            public Task<Response> RequestAsync( Request request )
            {
                return Task.FromResult( Request( request ) );
            }
        }
    }
}
