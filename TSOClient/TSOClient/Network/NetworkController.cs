﻿/*The contents of this file are subject to the Mozilla Public License Version 1.1
(the "License"); you may not use this file except in compliance with the
License. You may obtain a copy of the License at http://www.mozilla.org/MPL/

Software distributed under the License is distributed on an "AS IS" basis,
WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
the specific language governing rights and limitations under the License.

The Original Code is the TSOClient.

The Initial Developer of the Original Code is
ddfczm. All Rights Reserved.

Contributor(s): ______________________________________.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Threading;
using TSOClient.Code.UI.Controls;
using TSOClient.Events;
using TSOClient.Network.Events;
using GonzoNet;
using ProtocolAbstractionLibraryD;
using LogThis;

namespace TSOClient.Network
{
    public delegate void LoginProgressDelegate(int stage);
    public delegate void OnProgressDelegate(ProgressEvent e);
    public delegate void OnLoginStatusDelegate(LoginEvent e);

    public delegate void OnCharacterCreationProgressDelegate(CharacterCreationStatus CCStatus);
    public delegate void OnCharacterCreationStatusDelegate(CharacterCreationStatus CCStatus);
    public delegate void OnCityTokenDelegate(CityInfo SelectedCity);
    public delegate void OnCityTransferProgressDelegate(CityTransferStatus e);
    public delegate void OnCharacterRetirementDelegate(string CharacterName);

    /// <summary>
    /// Handles moving between various network states, e.g.
    /// Logging in, connecting to a city, connecting to a lot
    /// </summary>
    public class NetworkController
    {
        public event NetworkErrorDelegate OnNetworkError;
        public event OnProgressDelegate OnLoginProgress;
        public event OnLoginStatusDelegate OnLoginStatus;

        public event OnCharacterCreationProgressDelegate OnCharacterCreationProgress;
        public event OnCharacterCreationStatusDelegate OnCharacterCreationStatus;
        public event OnCityTokenDelegate OnCityToken;
        public event OnCityTransferProgressDelegate OnCityTransferProgress;
        public event OnCharacterRetirementDelegate OnCharacterRetirement;

        public NetworkController()
        {
        }

        public void Init(NetworkClient client)
        {
            client.OnNetworkError += new NetworkErrorDelegate(Client_OnNetworkError);
            GonzoNet.Logger.OnMessageLogged += new GonzoNet.MessageLoggedDelegate(Logger_OnMessageLogged);
            ProtocolAbstractionLibraryD.Logger.OnMessageLogged += new 
                ProtocolAbstractionLibraryD.MessageLoggedDelegate(Logger_OnMessageLogged);

            /** Register the various packet handlers **/
            /*client.On(PacketType.LOGIN_NOTIFY, new ReceivedPacketDelegate(_OnLoginNotify));
            client.On(PacketType.LOGIN_FAILURE, new ReceivedPacketDelegate(_OnLoginFailure));
            client.On(PacketType.CHARACTER_LIST, new ReceivedPacketDelegate(_OnCharacterList));
            client.On(PacketType.CITY_LIST, new ReceivedPacketDelegate(_OnCityList));*/
        }

        #region Log Sink

        private void Logger_OnMessageLogged(GonzoNet.LogMessage Msg)
        {
            Log.LogThis(Msg.Message, (eloglevel)Msg.Level);
        }

        private void Logger_OnMessageLogged(ProtocolAbstractionLibraryD.LogMessage Msg)
        {
            switch (Msg.Level)
            {
                case ProtocolAbstractionLibraryD.LogLevel.error:
                    Log.LogThis(Msg.Message, eloglevel.error);
                    break;
                case ProtocolAbstractionLibraryD.LogLevel.info:
                    Log.LogThis(Msg.Message, eloglevel.info);
                    break;
                case ProtocolAbstractionLibraryD.LogLevel.warn:
                    Log.LogThis(Msg.Message, eloglevel.warn);
                    break;
            }
        }

        #endregion

        public void _OnLoginNotify(NetworkClient Client, ProcessedPacket packet)
        {
            UIPacketHandlers.OnInitLoginNotify(NetworkFacade.Client, packet);
            OnLoginProgress(new ProgressEvent(EventCodes.PROGRESS_UPDATE) { Done = 2, Total = 4 });
        }

        public void _OnLoginFailure(NetworkClient Client, ProcessedPacket packet)
        {
            UIPacketHandlers.OnLoginFailResponse(ref NetworkFacade.Client, packet);
            OnLoginStatus(new LoginEvent(EventCodes.LOGIN_RESULT) { Success = false, VersionOK = true });
        }

        public void _OnInvalidVersion(NetworkClient Client, ProcessedPacket packet)
        {
            UIPacketHandlers.OnInvalidVersionResponse(ref NetworkFacade.Client, packet);
            OnLoginStatus(new LoginEvent(EventCodes.LOGIN_RESULT) { Success = false, VersionOK = false });
        }

        /// <summary>
        /// Received list of characters for account from login server.
        /// </summary>
        public void _OnCharacterList(NetworkClient Client, ProcessedPacket packet)
        {
            OnLoginProgress(new ProgressEvent(EventCodes.PROGRESS_UPDATE) { Done = 3, Total = 4 });
            UIPacketHandlers.OnCharacterInfoResponse(packet, NetworkFacade.Client);
        }

        /// <summary>
        /// Received a list of available cities from the login server.
        /// </summary>
        public void _OnCityList(NetworkClient Client, ProcessedPacket packet)
        {
            UIPacketHandlers.OnCityInfoResponse(packet);
            OnLoginProgress(new ProgressEvent(EventCodes.PROGRESS_UPDATE) { Done = 4, Total = 4 });
            OnLoginStatus(new LoginEvent(EventCodes.LOGIN_RESULT) { Success = true });
        }

        /// <summary>
        /// Progressing to city server.
        /// </summary>
        public void _OnCharacterCreationProgress(NetworkClient Client, ProcessedPacket packet)
        {
            CharacterCreationStatus CCStatus = UIPacketHandlers.OnCharacterCreationProgress(Client, packet);
            OnCharacterCreationProgress(CCStatus);
        }

        public void _OnCharacterCreationStatus(NetworkClient Client, ProcessedPacket packet)
        {
            CharacterCreationStatus CCStatus = UIPacketHandlers.OnCharacterCreationStatus(Client, packet);
            OnCharacterCreationStatus(CCStatus);
        }

        public void _OnCityToken(NetworkClient Client, ProcessedPacket packet)
        {
            UIPacketHandlers.OnCityToken(Client, packet);
            OnCityToken(PlayerAccount.CurrentlyActiveSim.ResidingCity);
        }

        public void _OnCityTokenResponse(NetworkClient Client, ProcessedPacket packet)
        {
            CityTransferStatus Status = UIPacketHandlers.OnCityTokenResponse(Client, packet);
            OnCityTransferProgress(Status);
        }

        public void _OnRetireCharacterStatus(NetworkClient Client, ProcessedPacket Packet)
        {
            string CharacterName = UIPacketHandlers.OnCharacterRetirement(Client, Packet);
            OnCharacterRetirement(CharacterName);
        }

        /// <summary>
        /// Authenticate with the service client to get a token,
        /// Then get info about avatars & cities
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public void InitialConnect(string username, string password)
        {
            var client = NetworkFacade.Client;
            LoginArgsContainer Args = new LoginArgsContainer();
            Args.Username = username;
            Args.Password = password;
            Args.Enc = new GonzoNet.Encryption.ARC4Encryptor(password);
            Args.Client = client;

            client.Connect(Args);
        }
        
        /// <summary>
        /// Reconnects to a CityServer.
        /// </summary>
        public void Reconnect(ref NetworkClient Client, CityInfo SelectedCity, LoginArgsContainer LoginArgs)
        {
            Client.Disconnect();
            Client.Connect(LoginArgs);
        }

        private void Client_OnNetworkError(SocketException Exception)
        {
            OnNetworkError(Exception);
        }

        /// <summary>
        /// Logout of the game & service client
        /// </summary>
        public void Logout()
        {

        }
    }
}
