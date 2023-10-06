﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Azure.IIoT.OpcUa.Publisher.Tests.Stack
{
    using Azure.IIoT.OpcUa.Publisher.Models;
    using Azure.IIoT.OpcUa.Publisher.Stack;
    using Autofac;
    using Furly.Exceptions;
    using Furly.Extensions.Serializers.Json;
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Xunit;
    using Opc.Ua;

    public class OpcUaApplicationTests
    {
        [Fact]
        public async Task GetApplicationCertificateTest1Async()
        {
            using var bootstrap = Build();
            var oldCerts = bootstrap.Resolve<IOpcUaCertificates>();
            await CleanAsync(oldCerts, CertificateStoreName.Application);

            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Application, true);
            var own = Assert.Single(certificates);
            Assert.True(own.HasPrivateKey);
        }

        [Fact]
        public async Task GetApplicationCertificateTest2Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.Application);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Application);
            Assert.Empty(certificates);

            using var newCert = CreateRSACertificate("test");
            await certs.AddCertificateAsync(CertificateStoreName.Application,
                newCert.Export(X509ContentType.Pfx, "pfx"), "pfx");

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Application, true);
            var own = Assert.Single(certificates);
            Assert.True(own.HasPrivateKey);
        }

        [Fact]
        public async Task GetTrustedCertificatesTest1Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.Trusted);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Empty(certificates);

            using var newCert = CreateRSACertificate("test");
            await certs.AddCertificateAsync(CertificateStoreName.Trusted,
                newCert.Export(X509ContentType.Pfx, "pfx"), "pfx");

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            var cert = Assert.Single(certificates);
            Assert.Equal(cert.Thumbprint, newCert.Thumbprint);

            await certs.RemoveCertificateAsync(CertificateStoreName.Trusted, newCert.Thumbprint);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Empty(certificates);
        }

        [Fact]
        public async Task GetTrustedCertificatesTest2Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.Trusted);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Empty(certificates);

            using var newCert = CreateRSACertificate("test");
            var pfx = newCert.Export(X509ContentType.Pfx, "pfx");
            await certs.AddCertificateAsync(CertificateStoreName.Trusted, pfx, "pfx");
            await certs.AddCertificateAsync(CertificateStoreName.Trusted, pfx, "pfx");
            await certs.AddCertificateAsync(CertificateStoreName.Trusted, pfx, "pfx");

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            var cert = Assert.Single(certificates);
            await CleanAsync(certs, CertificateStoreName.Trusted);
        }

        [Fact]
        public async Task GetTrustedCertificatesTest3Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.Trusted);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Empty(certificates);

            using var newCert1 = CreateRSACertificate("test1");
            using var newCert2 = CreateRSACertificate("test2");
            using var newCert3 = CreateRSACertificate("test3");
            await certs.AddCertificateAsync(CertificateStoreName.Trusted,
                newCert1.Export(X509ContentType.Pfx, "pfx"), "pfx");
            await certs.AddCertificateAsync(CertificateStoreName.Trusted,
                newCert2.Export(X509ContentType.Pfx, "pfx"), "pfx");
            await certs.AddCertificateAsync(CertificateStoreName.Trusted,
                newCert3.Export(X509ContentType.Pfx, "pfx"), "pfx");

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Equal(3, certificates.Count);
            await CleanAsync(certs, CertificateStoreName.Trusted);
        }

        [Fact]
        public async Task GetTrustedCertificatesTest4Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.Trusted);
            await CleanAsync(certs, CertificateStoreName.Issuer);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Empty(certificates);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Issuer);
            Assert.Empty(certificates);

            using var newCert1 = CreateRSACertificate("test1");
            using var newCert2 = CreateRSACertificate("test2");
            using var newCert3 = CreateRSACertificate("test3");
            var chain = newCert1.RawData.Concat(newCert2.RawData).Concat(newCert3.RawData).ToArray();

            await certs.AddCertificateChainAsync(chain);

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Single(certificates);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Issuer);
            Assert.Equal(2, certificates.Count);
            await CleanAsync(certs, CertificateStoreName.Trusted);
            await CleanAsync(certs, CertificateStoreName.Issuer);
        }

        [Fact]
        public async Task GetTrustedHttpsCertificatesTestAsync()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.Https);
            await CleanAsync(certs, CertificateStoreName.HttpsIssuer);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Https);
            Assert.Empty(certificates);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.HttpsIssuer);
            Assert.Empty(certificates);

            using var newCert1 = CreateRSACertificate("test1");
            using var newCert2 = CreateRSACertificate("test2");
            using var newCert3 = CreateRSACertificate("test3");
            var chain = newCert1.RawData.Concat(newCert2.RawData).Concat(newCert3.RawData).ToArray();

            await certs.AddCertificateChainAsync(chain, true);

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Https);
            Assert.Single(certificates);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.HttpsIssuer);
            Assert.Equal(2, certificates.Count);
            await CleanAsync(certs, CertificateStoreName.Https);
            await CleanAsync(certs, CertificateStoreName.HttpsIssuer);
        }

        [Fact]
        public async Task ApproveRejectedCertificateTestAsync()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.Trusted);
            await CleanAsync(certs, CertificateStoreName.Rejected);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Empty(certificates);

            using var rejectedCert = CreateRSACertificate("test1");
            await certs.AddCertificateAsync(CertificateStoreName.Rejected,
                rejectedCert.Export(X509ContentType.Pfx, "pfx"), "pfx");
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Rejected);
            Assert.Single(certificates);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            Assert.Empty(certificates);

            await certs.ApproveRejectedCertificateAsync(rejectedCert.Thumbprint);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Rejected);
            Assert.Empty(certificates);
            certificates = await certs.ListCertificatesAsync(CertificateStoreName.Trusted);
            var approved = Assert.Single(certificates);
            Assert.Equal(approved.Thumbprint, rejectedCert.Thumbprint);

            await CleanAsync(certs, CertificateStoreName.Rejected);
            await CleanAsync(certs, CertificateStoreName.Trusted);
        }

        [Fact]
        public async Task ApproveRejectedCertificateNotFoundTestAsync()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            using var rejectedCert = CreateRSACertificate("test1");
            await Assert.ThrowsAsync<ResourceNotFoundException>(
                async () => await certs.ApproveRejectedCertificateAsync(rejectedCert.Thumbprint));
        }

        [Fact]
        public async Task RemoveCertificateNotFoundTestAsync()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            using var rejectedCert = CreateRSACertificate("test1");
            await Assert.ThrowsAsync<ResourceNotFoundException>(
                async () => await certs.RemoveCertificateAsync(
                    CertificateStoreName.Trusted, rejectedCert.Thumbprint));
        }

        [Fact]
        public async Task GetUserCertificateTest1Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.User);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.User);
            Assert.Empty(certificates);

            using var newCert1 = CreateRSACertificate("user1");
            using var newCert2 = CreateRSACertificate("user2");
            using var newCert3 = CreateRSACertificate("user3");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert1.Export(X509ContentType.Pfx, "pfx1"), "pfx1");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert2.Export(X509ContentType.Pfx, "pfx2"), "pfx2");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert3.Export(X509ContentType.Pfx, "pfx3"), "pfx3");

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.User, true);
            Assert.Equal(3, certificates.Count);
            Assert.All(certificates, c => Assert.False(c.HasPrivateKey));

            var config = container.Resolve<IOpcUaConfiguration>();

            var credential = new CredentialModel
            {
                Type = CredentialType.X509Certificate,
                Value = new DefaultJsonSerializer().FromObject(new
                {
                    user = "DC=user2",
                    password = "pfx2"
                })
            };
            var identity = await credential.ToUserIdentityAsync(config.Value);
            Assert.NotNull(identity);
            Assert.Equal(UserTokenType.Certificate, identity.TokenType);
            var x509Token = identity.GetIdentityToken() as X509IdentityToken;
            Assert.NotNull(x509Token);
            Assert.True(x509Token.Certificate.HasPrivateKey);
            Assert.Equal(newCert2.Thumbprint, x509Token.Certificate.Thumbprint);
        }

        [Fact]
        public async Task GetUserCertificateTest2Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.User);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.User);
            Assert.Empty(certificates);

            using var newCert1 = CreateRSACertificate("user1");
            using var newCert2 = CreateRSACertificate("user2");
            using var newCert3 = CreateRSACertificate("user3");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert1.Export(X509ContentType.Pfx, "pfx1"), "pfx1");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert2.Export(X509ContentType.Pfx, "pfx2"), "pfx2");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert3.Export(X509ContentType.Pfx, "pfx3"), "pfx3");

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.User, true);
            Assert.Equal(3, certificates.Count);
            Assert.All(certificates, c => Assert.False(c.HasPrivateKey));

            var config = container.Resolve<IOpcUaConfiguration>();
            var credential = new CredentialModel
            {
                Type = CredentialType.X509Certificate,
                Value = new DefaultJsonSerializer().FromObject(new
                {
                    thumbprint = newCert3.Thumbprint,
                    password = "pfx3"
                })
            };
            var identity = await credential.ToUserIdentityAsync(config.Value);
            Assert.NotNull(identity);
            Assert.Equal(UserTokenType.Certificate, identity.TokenType);
            var x509Token = identity.GetIdentityToken() as X509IdentityToken;
            Assert.NotNull(x509Token);
            Assert.True(x509Token.Certificate.HasPrivateKey);
            Assert.Equal(newCert3.Thumbprint, x509Token.Certificate.Thumbprint);
        }

        [Fact]
        public async Task GetUserCertificateTest3Async()
        {
            using var container = Build();
            var certs = container.Resolve<IOpcUaCertificates>();
            await CleanAsync(certs, CertificateStoreName.User);
            var certificates = await certs.ListCertificatesAsync(CertificateStoreName.User);
            Assert.Empty(certificates);

            using var newCert1 = CreateRSACertificate("user1");
            using var newCert2 = CreateRSACertificate("user2");
            using var newCert3 = CreateRSACertificate("user3");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert1.Export(X509ContentType.Pfx, "pfx1"), "pfx1");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert2.Export(X509ContentType.Pfx, "pfx2"), "pfx2");
            await certs.AddCertificateAsync(CertificateStoreName.User,
                newCert3.Export(X509ContentType.Pfx, "pfx3"), "pfx3");

            certificates = await certs.ListCertificatesAsync(CertificateStoreName.User, true);
            Assert.Equal(3, certificates.Count);
            Assert.All(certificates, c => Assert.False(c.HasPrivateKey));

            var config = container.Resolve<IOpcUaConfiguration>();
            var credential = new CredentialModel
            {
                Type = CredentialType.X509Certificate,
                Value = new DefaultJsonSerializer().FromObject(new
                {
                    thumbprint = newCert3.Thumbprint,
                    password = "wrong"
                })
            };
            var ex = await Assert.ThrowsAsync<ServiceResultException>(
                async () => await credential.ToUserIdentityAsync(config.Value));
            Assert.Equal(StatusCodes.BadCertificateInvalid, ex.StatusCode);

            config = container.Resolve<IOpcUaConfiguration>();
            credential = new CredentialModel
            {
                Type = CredentialType.X509Certificate,
                Value = new DefaultJsonSerializer().FromObject(new
                {
                    password = "pfx3"
                })
            };
            ex = await Assert.ThrowsAsync<ServiceResultException>(
                async () => await credential.ToUserIdentityAsync(config.Value));
            Assert.Equal(StatusCodes.BadNotSupported, ex.StatusCode);

            config = container.Resolve<IOpcUaConfiguration>();
            credential = new CredentialModel
            {
                Type = CredentialType.X509Certificate,
                Value = new DefaultJsonSerializer().FromObject(new
                {
                    thumbprint = newCert3.Thumbprint,
                })
            };
            ex = await Assert.ThrowsAsync<ServiceResultException>(
                async () => await credential.ToUserIdentityAsync(config.Value));
            Assert.Equal(StatusCodes.BadCertificateInvalid, ex.StatusCode);
        }

        private static async Task CleanAsync(IOpcUaCertificates certs, CertificateStoreName store)
        {
            var certificates = await certs.ListCertificatesAsync(store);
            foreach (var c in certificates)
            {
                await certs.RemoveCertificateAsync(store, c.Thumbprint);
            }
        }

        private static X509Certificate2 CreateRSACertificate(string name)
        {
            using var rsa = RSA.Create();
            var req = new CertificateRequest("DC=" + name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            return req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddHours(5));
        }

        private static IContainer Build()
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.AddLogging();
            containerBuilder.AddOpcUaStack();
            return containerBuilder.Build();
        }
    }
}