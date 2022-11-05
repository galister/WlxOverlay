#version 330 core
in vec2 fUv;

uniform sampler2D uTexture0;
uniform vec4 uColor;

out vec4 FragColor;

void main()
{
    float r = texture(uTexture0, fUv).r;
    FragColor = vec4(r,r,r,r) * uColor;
}