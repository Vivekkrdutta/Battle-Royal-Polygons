# Linework Lite

## About Linework Lite

Linework Lite is a free Unity package that allows you to render high-quality outlines and fill effects.

For higher quality outlines, edge detection and surface fill effects, check out [the complete version of Linework](https://assetstore.unity.com/packages/vfx/shaders/linework-294140). The complete version als includes:

- **Soft Outline:** Renders outlines by generating a silhouette of an object and applying a dilation/blur effect, resulting in smooth, soft-edged contours around objects.
- **Wide Outline:** Renders an outline by generating a signed distance field (SDF) for each object and then sampling it. This creates consistent outlines that smoothly follows the shape of an object.
- **Edge Detection:** Renders outlines by detecting edges and discontinuities within the scene, such as differences in *depth*, *normal vector*, *color*, or *custom input buffers*. This process creates a consistent outline effect that is applied uniformly across the entire scene, making it suitable for both external and internal object boundaries.
- **Surface Fill:** Renders fills by rendering an object with a fill material.

[Documentation](https://linework.ameye.dev) • [Asset Store](https://assetstore.unity.com/packages/vfx/shaders/free-outline-326925) • [Discord](https://discord.gg/cFfQGzQdPn)

## License

The source code included with this asset can be freely modified to suit your needs. However, please adhere to the following restrictions:

- Do **not** upload the source code to any public repository (e.g., GitHub). You may omit the files or keep the repository private.
- Do **not** use any part of this source code in new or existing publications on the Asset Store.
- Do **not** resell the source code or the compiled version of it, either in full or in part. You can include the compiled version of the source code as an integrated component of your game.

Redistribution of Linework is **not** allowed. If you obtained a copy through other channels than the Asset Store, please respect my work of developing/maintaining Linework by purchasing a legitimate copy from the Asset Store.

[Asset Store](https://assetstore.unity.com/packages/vfx/shaders/free-outline-326925)

## Quick Start

After importing the asset into Unity, you will be greeted by the support window. In the *Configure* tab, click on the *Detect* button. This will verify that everything is set up correctly in your project. If the support window does not open, you can open it by clicking *Window > Linework > About and Support*.

If the result is showing only green checkmarks, you are good to go! If not, see the [Troubleshooting and Known Limitations](https://linework.ameye.dev/known-limitations/) section.

To get started, open the *Universal Renderer Data* asset, click on *Add Renderer Feature* and select the outline/fill effect that you would like to add. Each outline effect stores its settings in a separate object that you can create somewhere in your Assets folder, by right-clicking and selecting *Create > Linework > Outline Settings*.

Drag the created settings into the object slot of the renderer feature. You can now click the *Open* button to open the settings.

By default, the outline should be applied to the whole scene.

Depending on which outline/fill effect you are using, you can find more detailed information about the different configuration options in the [Documentation](https://linework.ameye.dev).

## Features

Linework Lite contains a single renderer feature to render outlines.

- **Free Outline:** Renders outlines by rendering an extruded version of an object behind the original object.

For higher quality outlines, edge detection and surface fill effects, check out [the complete version of Linework](https://assetstore.unity.com/packages/vfx/shaders/linework-294140). The complete version als includes:

- **Soft Outline:** Renders outlines by generating a silhouette of an object and applying a dilation/blur effect, resulting in smooth, soft-edged contours around objects.
- **Wide Outline:** Renders an outline by generating a signed distance field (SDF) for each object and then sampling it. This creates consistent outlines that smoothly follows the shape of an object.
- **Edge Detection:** Renders outlines by detecting edges and discontinuities within the scene, such as differences in *depth*, *normal vector*, *color*, or *custom input buffers*. This process creates a consistent outline effect that is applied uniformly across the entire scene, making it suitable for both external and internal object boundaries.
- **Surface Fill:** Renders fills by rendering an object with a fill material.

Each effect has an extensive range of settings such as which objects the outline is applied to, the visuals of the outline and the behavior of the outline.

## Compatibility

Linework Lite is compatible with **Unity 6** and the **Universal Render Pipeline**. Other combinations are not supported.

## Contact

[Discord](https://discord.gg/cFfQGzQdPn) • [@alexanderameye](https://twitter.com/alexanderameye) • [https://ameye.dev](https://ameye.dev)
