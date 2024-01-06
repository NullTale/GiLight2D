# GiLight2D

[![Twitter](https://img.shields.io/badge/Follow-Twitter?logo=twitter&color=white)](https://twitter.com/NullTale)
[![Boosty](https://img.shields.io/badge/Support-Boosty?logo=boosty&color=white)](https://boosty.to/nulltale)

Unity Urp Render Feature for 2D Global Illumination.<br>
GiLight can be used both for conventional lighting and to create stylized visual effects ✨<br>
WebGl https://nulltale.itch.io/light-room<br>

[![Asset Store](https://img.shields.io/badge/Asset%20Store-asd?logo=Unity&color=red)](https://assetstore.unity.com/packages/tools/particles-effects/gilight2d-268033)

![Gif](https://github.com/NullTale/GiLight2D/assets/1497430/d5eb3708-93e0-462a-829e-6931863ad2ad)



## What is it?
In practice, this is an approach to lighting with rays, in which glow and shadows are created naturally, and the number of light sources is not limited.<br>
Implemented quite enough options, tested with Unity 2022.<br>

#### Some of the options
* fixed resolution for pixel art.
* control via post process volume.
* render texture output.
* padding the screen borders to display objects outside the camera.
* different noise settings and resolution.
* etc.


## Volume control
Various lighting configurations can be mixed to create certain kinds of scene effects.

![Gif](https://github.com/NullTale/GiLight2D/assets/1497430/1f9ccdcc-5e28-4f07-bd54-d4e3ceff2d09)

## Installation and use
Install via PackageManager `https://github.com/NullTale/GiLight2D.git`

<img src="https://user-images.githubusercontent.com/1497430/213906801-7cab3334-5626-46b8-9966-d5c0b6107edc.png">

Add `GiLight2DFeature` to the Urp Renderer (urp must be configured).<br>
If the scene is empty this should already be enough to see the effect.

<img src="https://user-images.githubusercontent.com/1497430/213907330-64d37b07-2833-4f8e-8b62-88455c05d604.png"><br>
<sup>Three sprites on an empty scene.</sup>

Then configure the object mask and the output texture, which can then be used in the shader (the name must match).

![image](https://user-images.githubusercontent.com/1497430/213999888-f368c057-cbd9-4af2-ac4e-bc745f692033.png)

Now the texture can be used from the shader.<br> 
> UrpRenderer is configured so that it doesn't draw Gi objects by mask.<br> 
> Texture `Exposed` checkbox must be unchecked.

![image](https://user-images.githubusercontent.com/1497430/213909802-45824d6d-7307-416f-b6f9-caebc7f45032.png)<br>
<sup>The sprite of the square uses the Gi texture from the screen coordinates.</sup>

## How it works?
The general idea is that from each pixel rays are released in all directions, from the sum of the rays that hit the light source is the final color of the pixel.<Br> In the end a lighting texture should be calculated which can then be used.<Br> With this approach glow and shadow are formed naturally.
  
![Rays](https://user-images.githubusercontent.com/1497430/214540599-eb907420-0655-4029-b54e-3484a69e4b31.gif)


In order to effectively calculate the rays a sdf map is created, in fact it is a distance map to the nearest light source, it is used to get the offset distance of the ray when searching for the light source.
So the whole rendering process consists of three steps: rendering objects into buffer, creating a distance map using jfa algorithm, applying gi shader.
Ray bounces are counted by calculating the illumination for the pixels on the border of the objects.
  
![Pipeline](https://user-images.githubusercontent.com/1497430/214540624-e9e66d99-6076-4345-9e2b-1996050e594f.gif)

Almost all the calculations are performed in a fragment shader, from which the number of objects does not affect performance, but strongly depends on the resolution.
