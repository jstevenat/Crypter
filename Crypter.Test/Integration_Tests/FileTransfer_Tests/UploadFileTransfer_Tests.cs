﻿/*
 * Copyright (C) 2023 Crypter File Transfer
 * 
 * This file is part of the Crypter file transfer project.
 * 
 * Crypter is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * The Crypter source code is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * You can be released from the requirements of the aforementioned license
 * by purchasing a commercial license. Buying such a license is mandatory
 * as soon as you develop commercial activities involving the Crypter source
 * code without disclosing the source code of your own applications.
 * 
 * Contact the current copyright holder to discuss commercial license options.
 */

using Crypter.Common.Client.Interfaces;
using Crypter.Common.Client.Interfaces.Repositories;
using Crypter.Common.Contracts.Features.Transfer;
using Crypter.Common.Contracts.Features.UserAuthentication;
using Crypter.Common.Enums;
using Crypter.Common.Monads;
using Crypter.Crypto.Common.StreamEncryption;
using Crypter.Test.Integration_Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Crypter.Test.Integration_Tests.FileTransfer_Tests
{
   [TestFixture]
   internal class UploadFileTransfer_Tests
   {
      private Setup _setup;
      private WebApplicationFactory<Program> _factory;
      private ICrypterApiClient _client;
      private ITokenRepository _clientTokenRepository;

      [OneTimeSetUp]
      public async Task OneTimeSetUp()
      {
         _setup = new Setup();
         await _setup.InitializeRespawnerAsync();

         _factory = await Setup.SetupWebApplicationFactoryAsync();
         (_client, _clientTokenRepository) = Setup.SetupCrypterApiClient(_factory.CreateClient());
      }

      [TearDown]
      public async Task TearDown()
      {
         await _setup.ResetServerDataAsync();
      }

      [OneTimeTearDown]
      public async Task OneTimeTearDown()
      {
         await _factory.DisposeAsync();
      }

      [Test]
      public async Task Upload_Anonymous_File_Transfer_Works()
      {
         (EncryptionStream encryptionStream, byte[] keyExchangeProof) = TestData.GetDefaultEncryptionStream();
         UploadFileTransferRequest request = new UploadFileTransferRequest(TestData.DefaultTransferFileName, TestData.DefaultTransferFileContentType, TestData.DefaultPublicKey, TestData.DefaultKeyExchangeNonce, keyExchangeProof, TestData.DefaultTransferLifetimeHours);
         var result = await _client.FileTransfer.UploadFileTransferAsync(Maybe<string>.None, request, encryptionStream, false);

         Assert.True(result.IsRight);
      }

      [TestCase(true, false)]
      [TestCase(false, true)]
      [TestCase(true, true)]
      public async Task Upload_User_File_Transfer_Works(bool senderDefined, bool recipientDefined)
      {
         Maybe<string> senderUsername = senderDefined
            ? TestData.DefaultUsername
            : Maybe<string>.None;
         const string senderPassword = TestData.DefaultPassword;

         Maybe<string> recipientUsername = recipientDefined
            ? "Samwise"
            : Maybe<string>.None;
         const string recipientPassword = "dropping_eaves";

         Assert.True((senderDefined == false && senderUsername.IsNone)
            || (senderDefined && senderUsername.IsSome));

         await senderUsername.IfSomeAsync(async username =>
         {
            RegistrationRequest registrationRequest = TestData.GetRegistrationRequest(username, senderPassword);
            var registrationResult = await _client.UserAuthentication.RegisterAsync(registrationRequest);

            LoginRequest loginRequest = TestData.GetLoginRequest(username, senderPassword, TokenType.Session);
            var loginResult = await _client.UserAuthentication.LoginAsync(loginRequest);

            await loginResult.DoRightAsync(async loginResponse =>
            {
               await _clientTokenRepository.StoreAuthenticationTokenAsync(loginResponse.AuthenticationToken);
               await _clientTokenRepository.StoreRefreshTokenAsync(loginResponse.RefreshToken, TokenType.Session);
            });

            Assert.True(registrationResult.IsRight);
            Assert.True(loginResult.IsRight);
         });

         Assert.True((recipientDefined == false && recipientUsername.IsNone)
            || (recipientDefined && recipientUsername.IsSome));

         await recipientUsername.IfSomeAsync(async username =>
         {
            RegistrationRequest registrationRequest = TestData.GetRegistrationRequest(username, recipientPassword);
            var registrationResult = await _client.UserAuthentication.RegisterAsync(registrationRequest);
         });

         (EncryptionStream encryptionStream, byte[] keyExchangeProof) = TestData.GetDefaultEncryptionStream();
         UploadFileTransferRequest request = new UploadFileTransferRequest(TestData.DefaultTransferFileName, TestData.DefaultTransferFileContentType, TestData.DefaultPublicKey, TestData.DefaultKeyExchangeNonce, keyExchangeProof, TestData.DefaultTransferLifetimeHours);
         var result = await _client.FileTransfer.UploadFileTransferAsync(recipientUsername, request, encryptionStream, senderDefined);

         Assert.True(result.IsRight);
      }

      [Test]
      public async Task Upload_User_File_Transfer_Fails_When_Recipient_Does_Not_Exist()
      {
         (EncryptionStream encryptionStream, byte[] keyExchangeProof) = TestData.GetDefaultEncryptionStream();
         UploadFileTransferRequest request = new UploadFileTransferRequest(TestData.DefaultTransferFileName, TestData.DefaultTransferFileContentType, TestData.DefaultPublicKey, TestData.DefaultKeyExchangeNonce, keyExchangeProof, TestData.DefaultTransferLifetimeHours);
         var result = await _client.FileTransfer.UploadFileTransferAsync("John Smith", request, encryptionStream, false);

         Assert.True(result.IsLeft);
      }

      [Test]
      public void Upload_Authenticated_File_Transfer_Throws_When_Not_Authenticated()
      {
         (EncryptionStream encryptionStream, byte[] keyExchangeProof) = TestData.GetDefaultEncryptionStream();
         UploadFileTransferRequest request = new UploadFileTransferRequest(TestData.DefaultTransferFileName, TestData.DefaultTransferFileContentType, TestData.DefaultPublicKey, TestData.DefaultKeyExchangeNonce, keyExchangeProof, TestData.DefaultTransferLifetimeHours);

         Assert.ThrowsAsync<InvalidOperationException>(async () => await _client.FileTransfer.UploadFileTransferAsync(Maybe<string>.None, request, encryptionStream, true));
      }
   }
}
