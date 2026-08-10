# Arm art template

Files to paint (already exist as placeholders, overwrite in place):

    Textures/CA/ArmLeft_south.png     Textures/CA/ArmRight_south.png
    Textures/CA/ArmLeft_north.png     Textures/CA/ArmRight_north.png
    Textures/CA/ArmLeft_east.png      Textures/CA/ArmRight_east.png

West is the east file auto-mirrored by the game - do not make west files.

## Canvas contract

- 256x256, transparent background. Rendered in game at 0.5x0.5 tiles, no stretching.
- The canvas CENTER sits on the pawn's shoulder line (the node anchor). The code then
  nudges each side outward: left arm -0.27 tiles x, right +0.27 (south/north), tighter
  on profile facings. Paint the arm roughly centered; position tuning happens in code.
- See arm-reference.png here for the guide overlay (body extent circle + anchor cross).

## Color contract

- Paint in WHITE / GRAYS. The game multiplies the texture by each pawn's skin color
  (colorType Skin, skin shader). White = full skin tone, gray = shadowed skin.
- Alpha carries the shape. Anything opaque renders; keep sleeves/props out - skin only.

## Layering (already coded, tune numbers in ArmsNodeModule.cs)

- south/east/west: layer 35 (over body and torso apparel)
- north: layer 4 (arms tuck behind the body)
- Babies: no arms. Children: auto-scaled by life-stage body factor.

## Turning it on

ModEntry.cs, one commented line:

    // ArmsNodePatch.TryInstall(harmony);

Uncomment, build (dotnet build Source/ColonistAwareness.csproj), restart the game.
