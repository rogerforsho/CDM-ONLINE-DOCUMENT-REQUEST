using Microsoft.AspNetCore.Mvc;

namespace ProjectCapstone.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public PaymentController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("Upload")]
        public async Task<IActionResult> UploadPayment()
        {
            try
            {
                var requestId = Request.Form["requestId"].ToString();
                var paymentMethod = Request.Form["paymentMethod"].ToString();
                var paymentProof = Request.Form.Files.GetFile("paymentProof");

                if (string.IsNullOrEmpty(requestId))
                {
                    return Ok(new { success = false, message = "Request ID is required" });
                }

                if (paymentProof == null)
                {
                    return Ok(new { success = false, message = "Payment proof is required" });
                }

                // Create directory
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "payments");
                Directory.CreateDirectory(uploadsPath);

                // Save file
                var fileName = $"{requestId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(paymentProof.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await paymentProof.CopyToAsync(stream);
                }

                return Ok(new
                {
                    success = true,
                    message = "Payment uploaded successfully!",
                    fileName = fileName
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}
