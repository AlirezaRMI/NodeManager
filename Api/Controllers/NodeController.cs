using Application.Services.Interfaces;
using Domain.Models.Provision;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/provisioning")]
    public class ProvisioningController(INodeService service) : ControllerBase
    {
        [HttpPost("container")]
        [EndpointName("create Container")]
        [EndpointSummary("creates a new Xray container instance for a node.")]
        [ProducesResponseType(typeof(ProvisionResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ProvisionContainer([FromBody] ProvisionRequestDto request)
        {
            var response = await service.ProvisionContainerAsync(request);
            return Ok(response);
        }

        // [HttpDelete("container/{containerId}")]
        // [EndpointName("deprovision Container")]
        // [EndpointSummary("stops and deletes a Xray container instance.")]
        // [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        // [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        // public async Task<IActionResult> DeprovisionContainer([FromRoute] string containerId)
        // {
        //     var message = await service.DeprovisionContainerAsync(containerId);
        //     return Ok(message);
        // }

        [HttpGet("container/{containerId}/status")]
        [EndpointName("get Container Status")]
        [EndpointSummary("retrieves the current status of an Xray container instance.")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetContainerStatus([FromRoute] string containerId)
        {
            var status = await service.GetContainerStatusAsync(containerId);
            return Ok(status);
        }

        [HttpGet("container/{containerId}/logs")]
        [EndpointName("get Container Logs")]
        [EndpointSummary("retrieves logs from an Xray container instance.")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetContainerLogs([FromRoute] string containerId)
        {
            string logs = await service.GetContainerLogsAsync(containerId);
            return Ok(logs);
        }

        [HttpPost("container/{containerId}/pause")]
        [EndpointName("pause Container")]
        [EndpointSummary("pauses the specified container.")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PauseContainer([FromRoute] string containerId)
        {
            return Ok(await service.PauseContainerAsync(containerId));
        }

        [HttpPost("container/{containerId}/resume")]
        [EndpointName("resume Container")]
        [EndpointSummary("resumes (unpauses) the specified container.")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResumeContainer([FromRoute] string containerId)
        {
            return Ok(await service.ResumeContainerAsync(containerId));
        }
        
    }
}