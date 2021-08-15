# Video-ReEncoder

This is also a work in progress if you have suggestions or fixes free to message me or issue a merge request.

The _Video-ReEncoder_ application is developed for the purpose of encoding media at a certain [VMAF](https://github.com/Netflix/vmaf) perceptual quality level, an algorithm developed by netflix. In addition to utilizing [FFMPEG](https://www.ffmpeg.org/) to Re-Encode the media to a specific format. For use for compressing precious memories in a way where the information is preserved while allowing the smallest amount of filesize, this might also require a powerful computer to view the compressed files.

The licences of the used technologies are held by their respective owners, as outlined by the [LICENSE](LICENSE.md) File.

## Usage
This application is useful for testing, not production ready. Utilizes CRF for a consistent quality throughout encoded file, which is the recommended approach for Archival Purposes. Utilizes background threads with a priority set to idle to encode multiple files in tandem.

![Preview.png](.\Documents\Images\Preview.png)
**Input & Output Folder:** _Double Click_ > to select input folder of the media files will scan folders recursively.
**Start** > Begins the Operaton
**Encode Format** > Dropdown to allow for the user to select the encoder format, not all of the work. (Currently:  H265xCPU, H265xNvidia,  AV1xLibAOM)
**VMAF Target** > Target VMAF Quality to aim for.
**Overshoot** > How much percent to allow for overshoot, lower amount, higher likelihood the qualities between encoded files will be very close. (If not found in the range the closest encode value will be chosen.)

### Limitations
*The best solution is never to keep all original footage of media, I do not recommend re-encoding precious or ireplaceable files as the quality will never be the same and some data loss will occur.*

- Not currently optimized for encoding larger length videos, as over the cource of the encode some VMAF averages may be lower while others higher, the overall VMAF may be retained but during certain scenes the quality may suffer. **See: Development Goals and Av1an** *for more information*
- Some files may end up larger than the original if the source is particullary grainy
- Every Encode will always add a loss in some level of quality either perceptable or otherwise, subsequent encodes will compound that data loss. 


## Features

### Target VMAF
A specific quality target for the output files.

### Logging
Provides various useful information about the current encoding process.

### Resume
A stopped encode can be resumed picking up where it left off ingoring files that exist in the folder, and continue searching for the last files target paramters based off of previous encodes.

### Multiple Systems
Designed with intention of computers being able to target the same input and output directories on a network and work together to shorten the development time.

### Multi-Threaded
Currently tries to utilize any unused processing power by utilizing multiple treads working on separate files to a maximum of the CPU Cores. (Should make a parameter/Configurable with a default.)

### Low Priority
Process threads are run in a lower priorty to allow for important work to be run in the background, while the encoder merely utilizes and unused power.

### Formats
This currently supports via FFMPEG current and future facing technologies h.265, AV1.

### Stamp Info
Retains all of the original information about the file including and trys to copy over the following Metadata:
 - Create Date
 - Date Modified
 - GPS Data
 - Final VMAF Value

## Development

**Goals:** Where I would like to see the project develop to for the future, not in any order.

1. I would like to breakdown each scene from within a video and encode them to the target VMAF and re-stich them together like Av1an does, this would provide, I feel, the best amount of file side savings with variable compression throughout the scenes.
2. Command Interface for launching the application, without window that can execute the process in the terminal.
3. Detect and Re-Encode Corrupted Data in the event the computer was suspended and then later resumed, and some frames were garbled.
4. Custom User specified Pass and Final Encodes.

## Known Bugs
- **Encode Format** _"H265xNvidia"_ will fail if you computer doesn't support Cuda Encoding, if selected error state is not currently handled.
- 

## Inspiration

The following is a list of projects that acted as inspiration for the development of this project to allow the end user granual control over end resulting file.
- [VMAF](https://github.com/Netflix/vmaf): Allows for one to determine the visual accuracy of a re-encoded clip, this will simplify the process of determining if the source matched the output 
- [Av1an](https://github.com/master-of-zen/Av1an): This is an alternative encoder which has a similar goal of providing high quality encodes of AV1 files at a target VAMF, at the time when i used it the downside i had was installation simplicity and getting the desired VMAF the result never quite hit the target, the benefit of this system is that it breaks down each scene, within a video and encodes them in parallel. 
- [NEAV1E](https://github.com/Alkl58/NotEnoughAV1Encodes): Alternatives for AV1 Encoding which I consider to be the next best encode format for long term storage.
