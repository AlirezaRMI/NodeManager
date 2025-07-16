using Api.Filters;
using Application.Services.Interfaces;
using Domain.Models.Provision;
using Microsoft.AspNetCore.Mvc;


namespace Api.Controllers
{
    [ApiController]
    [Route("api/provisioning")]
    public class ProvisioningController(INodeService service, ILogger<ProvisioningController> logger) : ControllerBase
    {

        [HttpPost("container")]
        [EndpointName("create container")]
        [EndpointSummary("creates a new Xray container instance for a node.")]
        [ProducesResponseType(typeof(ProvisionResponseDto), StatusCodes.Status200OK)] 
        [ProducesResponseType(typeof(ProvisionResponseDto), StatusCodes.Status201Created)] 
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)] 
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)] 
        public async Task<IActionResult> ProvisionContainer([FromBody] ProvisionRequestDto request)
        {
            var response = await service.ProvisionContainerAsync(request);
            return Ok( response);
        }
        
        [HttpDelete("container/{containerId}")] 
        [EndpointName("deprovision container")]
        [EndpointSummary("sops and deletes an Xray container instance.")]
        [ProducesResponseType(typeof(ApiResult<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeprovisionContainer(long containerId) 
        {
            var message = await service.DeprovisionContainerAsync(containerId);
            return Ok(message);
        }
        
        [HttpGet("container/{containerId}/status")] 
        [EndpointName("get container status")]
        [EndpointSummary("retrieves the current status of an Xray container instance.")]
        [ProducesResponseType(typeof(ApiResult<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetContainerStatus(long containerId)
        {
            var status = await service.GetContainerStatusAsync(containerId);
            return Ok(status);
        }
        [HttpGet("container/{containerId}/logs")]
        [EndpointName("get container logs")]
        [EndpointSummary("retrieves logs from an Xray container instance.")]
        [ProducesResponseType(typeof(ApiResult<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetContainerLogs(long containerId)
        {
            string logs = await service.GetContainerLogsAsync(containerId);
            return Ok(logs);
        }
    }
}