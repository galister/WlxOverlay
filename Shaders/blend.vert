#version 450 core

in vec3 in_position;
in vec2 in_texCoord;

out vec2 texCoord;

void main()
{
    texCoord = in_texCoord;
    gl_Position = vec4(in_position, 1.0);
}