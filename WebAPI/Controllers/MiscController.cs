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

        [HttpGet]
        [Route("sleep/{s}")]
        public async Task<ActionResult> SleepSeconds(int s)
        {
            await Task.Delay(s*1000);
            return Ok();
        }

        [HttpPost]
        [Route("sleep/{s}")]
        public async Task<ActionResult> PostSleepSeconds(int s,[FromBody] object bodyData)
        {
            await Task.Delay(s * 1000);
            return Ok();
        }
    }
}
