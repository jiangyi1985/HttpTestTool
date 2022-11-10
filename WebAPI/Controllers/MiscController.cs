using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MiscController : ControllerBase
    {
        [HttpGet]
        [Route("sleep")]
        public async Task<ActionResult> Sleep()
        {
            await Task.Delay(3000);
            return Ok();
        }
    }
}
