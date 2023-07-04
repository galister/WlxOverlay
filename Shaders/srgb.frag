#version 330
in vec2 fUv;

uniform sampler2D uTexture0;

out vec4 FragColor;

void main()
{
    FragColor = texture(uTexture0, fUv);
    
    bvec4 cutoff = lessThan(FragColor, vec4(0.04045));
    vec4 higher = pow((FragColor + vec4(0.055))/vec4(1.055), vec4(2.4));
    vec4 lower = FragColor/vec4(12.92);

    FragColor = mix(higher, lower, cutoff);
}