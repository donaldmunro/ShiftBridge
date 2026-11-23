using System.Numerics;
using ShiftBridge.Core.Controllers.Sram;

namespace ShiftBridge.Core.Tests;

/// <summary>
/// Vecteurs publiés par GaborWnuk/sram-axs (PROTOCOL.md, appairage SRAMBond v1) : la seule vérité connue. Le déchiffrement du blob
/// publié (étiquette EAX authentique) valide à la fois le Diffie-Hellman (exposant PRIVÉ lu petit-boutiste, comme qdomyos-zwift)
/// et notre AES-EAX à en-tête vide.
/// </summary>
public class SramCryptoTests
{
    private static byte[] H(string hex) => Convert.FromHexString(hex);

    private static readonly byte[] ClientPriv = H("6418b20cb4e1d4cf4af19b184aff1d2a");
    private static readonly byte[] ClientPub = H("297ca1db5827261af813875fd09800b0");
    private static readonly byte[] DevicePub = H("9ac11ad0a4f6c2b99c5559e2d210c410");
    private static readonly byte[] Shared = H("55406a336a328156b81019d7ac3d5d24");
    private static readonly byte[] DeviceKey = H("b0690781867fde13ac1b9d30bbb4004f");

    [Fact]
    public void Modulus_is_2_pow_128_minus_713() =>
        Assert.Equal(BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFD37", System.Globalization.NumberStyles.HexNumber), SramCrypto.Modulus);

    [Fact]
    public void Public_key_matches_the_published_vector() =>
        Assert.Equal(ClientPub, SramCrypto.PublicKey(ClientPriv));

    [Fact]
    public void Shared_secret_matches_the_published_vector() =>
        Assert.Equal(Shared, SramCrypto.SharedSecret(DevicePub, ClientPriv));

    [Fact]
    public void Bond_blob_decrypts_to_the_published_device_key()
    {
        byte[] blob = [.. H("8d7a16ed42128ee445b9864f20324a0e"), .. H("0b9e1982be7a9ad3cf611f453696fa8d"), .. H("2f24f1878c44eb7f77ff5b4a3c616395")];
        Assert.Equal(DeviceKey, SramCrypto.DecryptBondBlob(Shared, blob));
    }

    [Fact]
    public void A_tampered_blob_or_wrong_secret_is_rejected()
    {
        byte[] blob = [.. H("8d7a16ed42128ee445b9864f20324a0e"), .. H("0b9e1982be7a9ad3cf611f453696fa8d"), .. H("2f24f1878c44eb7f77ff5b4a3c616395")];
        var bad = (byte[])blob.Clone();
        bad[20] ^= 1;
        Assert.Null(SramCrypto.DecryptBondBlob(Shared, bad));
        var wrong = (byte[])Shared.Clone();
        wrong[0] ^= 1;
        Assert.Null(SramCrypto.DecryptBondBlob(wrong, blob));
        Assert.Null(SramCrypto.DecryptBondBlob(Shared, blob[..47]));
    }

    [Fact]
    public void Both_sides_derive_the_same_secret()
    {
        var a = SramCrypto.NewPrivateKey();
        var b = SramCrypto.NewPrivateKey();
        Assert.Equal(SramCrypto.SharedSecret(SramCrypto.PublicKey(b), a), SramCrypto.SharedSecret(SramCrypto.PublicKey(a), b));
    }

    [Fact]
    public void Eax_round_trips_for_every_length_around_the_block_size()
    {
        var key = H("000102030405060708090a0b0c0d0e0f");
        var nonce = H("101112131415161718191a1b1c1d1e1f");
        for (int len = 0; len <= 40; len++)
        {
            var plain = Enumerable.Range(0, len).Select(i => (byte)(i * 7 + 1)).ToArray();
            var (ct, tag) = SramCrypto.EaxEncrypt(key, nonce, plain);
            Assert.Equal(len, ct.Length);
            Assert.Equal(plain, SramCrypto.EaxDecrypt(key, nonce, ct, tag));
        }
    }

    [Fact]
    public void Live_frame_decrypts_to_the_documented_gear_plaintext()
    {
        // Trame d'état : nonce (16) ‖ chiffré (9) ‖ étiquette (16). Texte clair du rapport 7 : a0 01 01 / a8 01 07 / b0 01 0c.
        var nonce = H("202122232425262728292a2b2c2d2e2f");
        var plain = H("a00101a80107b0010c");
        var (ct, tag) = SramCrypto.EaxEncrypt(DeviceKey, nonce, plain);
        byte[] frame = [.. nonce, .. ct, .. tag];
        Assert.Equal(41, frame.Length);
        Assert.Equal(plain, SramCrypto.DecryptFrame(DeviceKey, frame));
        frame[^1] ^= 1;
        Assert.Null(SramCrypto.DecryptFrame(DeviceKey, frame)); // clé périmée ou trame corrompue
    }
}
