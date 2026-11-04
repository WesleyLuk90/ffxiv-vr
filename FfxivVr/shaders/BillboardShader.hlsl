#define MAX_ANCHORS 50

cbuffer EyeProjection : register(b0)
{
    row_major float4x4 projection;
    row_major float4x4 inverseProjection;
    float4 screenUp;
    float4 worldUp;
    uint anchorCount;
    float4 anchors[MAX_ANCHORS];
    float4 targets[MAX_ANCHORS];
};

struct Vertex
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float2 uv0 : TEXCOORD0;
    float2 uv3 : TEXCOORD3;
    float4 uv1 : TEXCOORD1;
    float4 uv2 : TEXCOORD2;
};

static const float Epsilon = 1e-4;
static const float PlaneTolerance = 1e-3;
static const float MinPivotDistance = 0.5;

float3 toView(float4 clip)
{
    float4 view = mul(inverseProjection, clip);
    return view.xyz / view.w;
}

float3 rotateBetween(float3 from, float3 to, float3 v)
{
    float c = dot(from, to);
    if (c < Epsilon - 1)
    {
        return v;
    }
    float3 axis = cross(from, to);
    return v * c + cross(axis, v) + axis * (dot(axis, v) / (1 + c));
}

float3 alongPlane(float3 v, float3 normal)
{
    return v - normal * dot(v, normal);
}

int nearestAnchor(float3 center, float3 normal)
{
    int nearest = -1;
    float nearestDistance = 1e30;
    float planeTolerance = PlaneTolerance * length(center);
    for (uint i = 0; i < anchorCount; i++)
    {
        float3 anchor = anchors[i].xyz;
        float3 offset = anchor - center;
        if (abs(dot(offset, normal)) > planeTolerance)
        {
            continue;
        }
        float anchorDistance = length(offset);
        if (anchorDistance < nearestDistance)
        {
            nearest = i;
            nearestDistance = anchorDistance;
        }
    }
    return nearest;
}

[maxvertexcount(3)]
void main(triangle Vertex input[3], inout TriangleStream<Vertex> output)
{
    float3 view[3] = { toView(input[0].position), toView(input[1].position), toView(input[2].position) };
    float3 center = (view[0] + view[1]) * 0.5;
    float longest = distance(view[0], view[1]);
    if (distance(view[1], view[2]) > longest)
    {
        center = (view[1] + view[2]) * 0.5;
        longest = distance(view[1], view[2]);
    }
    if (distance(view[2], view[0]) > longest)
    {
        center = (view[2] + view[0]) * 0.5;
    }
    float3 normal = normalize(cross(view[1] - view[0], view[2] - view[0]));
    int index = nearestAnchor(center, normal);
    float3 anchor = index >= 0 ? anchors[index].xyz : center;
    float3 pivot = center;
    if (index >= 0)
    {
        pivot = dot(targets[index].xyz, anchor) > 0 ? targets[index].xyz : anchor;
        pivot = normalize(pivot) * max(length(pivot), MinPivotDistance);
    }
    float3 toEye = normalize(-pivot);
    if (dot(normal, toEye) < 0)
    {
        normal = -normal;
    }
    float3 facingUp = normalize(alongPlane(rotateBetween(normal, toEye, screenUp.xyz), toEye));
    float3 targetUp = alongPlane(worldUp.xyz, toEye);
    bool level = length(targetUp) > Epsilon;
    targetUp = level ? normalize(targetUp) : facingUp;
    float distanceScale = length(pivot) / max(length(anchor), Epsilon);
    float scale = distanceScale / max(abs(dot(normal, normalize(-anchor))), Epsilon);
    for (int i = 0; i < 3; i++)
    {
        Vertex vertex = input[i];
        float3 facing = rotateBetween(normal, toEye, view[i] - anchor) * scale;
        float3 rotated = pivot + rotateBetween(facingUp, targetUp, facing);
        vertex.position = mul(projection, float4(rotated, 1));
        output.Append(vertex);
    }
}
