#version 330
in vec2 fUv;

uniform sampler2D uTexture0;
uniform vec4 uColor;

out vec4 FragColor;

void main()
{
    FragColor = texture(uTexture0, fUv) * uColor;
}