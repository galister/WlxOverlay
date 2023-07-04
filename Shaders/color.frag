#version 330
in vec2 fUv;

uniform vec4 uColor;

out vec4 FragColor;

void main()
{
    FragColor = uColor;
}