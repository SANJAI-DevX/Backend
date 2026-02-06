using QRCoder;
using URLShortener.API.DTOs;

namespace URLShortener.API.Services
{
    public interface IQrCodeService
    {
        byte[] GenerateQrCode(QrCodeRequest request);
    }

    public class QrCodeService : IQrCodeService
    {
        public byte[] GenerateQrCode(QrCodeRequest request)
        {
            using var qrGenerator = new QRCodeGenerator();
            
            var eccLevel = request.ErrorCorrectionLevel switch
            {
                QrCodeErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
                QrCodeErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
                QrCodeErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
                QrCodeErrorCorrectionLevel.H => QRCodeGenerator.ECCLevel.H,
                _ => QRCodeGenerator.ECCLevel.M
            };

            using var qrCodeData = qrGenerator.CreateQrCode(request.Url, eccLevel);
            using var qrCode = new PngByteQRCode(qrCodeData);
            
            int pixelsPerModule = Math.Max(1, request.Size / 25);
            
            return qrCode.GetGraphic(pixelsPerModule);
        }
    }
}