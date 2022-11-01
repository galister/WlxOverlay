#version 450 core

in vec2 texCoord;
out vec4 fragColor;

uniform sampler2D mainTex;
uniform vec4 color;

void main(){
    fragColor = texture( mainTex, texCoord ) * color;
}