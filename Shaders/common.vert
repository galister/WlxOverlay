#version 330 core
layout (location = 0) in vec2 vPos;
layout (location = 1) in vec2 vUv;

uniform mat4 projection;

out vec2 fUv;

void main()
{
    fUv = vUv;
    gl_Position = projection * vec4(vPos, 1.0, 1.0);
}