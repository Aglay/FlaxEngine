// Copyright (c) 2012-2023 Wojciech Figat. All rights reserved.

#include "UICanvas.h"
#include "Engine/Scripting/MException.h"
#include "Engine/Scripting/ManagedCLR/MMethod.h"
#include "Engine/Scripting/ManagedCLR/MClass.h"
#include "Engine/Serialization/Serialization.h"
#if USE_MONO
#include <ThirdParty/mono-2.0/mono/metadata/appdomain.h>
#endif

#if COMPILE_WITHOUT_CSHARP
#define UICANVAS_INVOKE(event)
#else
// Cached methods (FlaxEngine.CSharp.dll is loaded only once)
MMethod* UICanvas_Serialize = nullptr;
MMethod* UICanvas_SerializeDiff = nullptr;
MMethod* UICanvas_Deserialize = nullptr;
MMethod* UICanvas_PostDeserialize = nullptr;
MMethod* UICanvas_Enable = nullptr;
MMethod* UICanvas_Disable = nullptr;
#if USE_EDITOR
MMethod* UICanvas_OnActiveInTreeChanged = nullptr;
#endif
MMethod* UICanvas_EndPlay = nullptr;
MMethod* UICanvas_ParentChanged = nullptr;

#define UICANVAS_INVOKE(event) \
    auto instance = GetManagedInstance(); \
    if (instance) \
    { \
	    MObject* exception = nullptr; \
	    UICanvas_##event->Invoke(instance, nullptr, &exception); \
	    if (exception) \
	    { \
		    MException ex(exception); \
		    ex.Log(LogType::Error, TEXT("UICanvas::" #event)); \
	    } \
    }
#endif

UICanvas::UICanvas(const SpawnParams& params)
    : Actor(params)
{
#if !COMPILE_WITHOUT_CSHARP
    Platform::MemoryBarrier();
    if (UICanvas_Serialize == nullptr)
    {
        MClass* mclass = GetClass();
        UICanvas_Serialize = mclass->GetMethod("Serialize");
        UICanvas_SerializeDiff = mclass->GetMethod("SerializeDiff", 1);
        UICanvas_Deserialize = mclass->GetMethod("Deserialize", 1);
        UICanvas_PostDeserialize = mclass->GetMethod("PostDeserialize");
        UICanvas_Enable = mclass->GetMethod("Enable");
        UICanvas_Disable = mclass->GetMethod("Disable");
#if USE_EDITOR
        UICanvas_OnActiveInTreeChanged = mclass->GetMethod("OnActiveInTreeChanged");
#endif
        UICanvas_EndPlay = mclass->GetMethod("EndPlay");
        UICanvas_ParentChanged = mclass->GetMethod("ParentChanged");
    }
#endif
}

#if USE_EDITOR

BoundingBox UICanvas::GetEditorBox() const
{
    const Vector3 size(50);
    return BoundingBox(_transform.Translation - size, _transform.Translation + size);
}

#endif

void UICanvas::Serialize(SerializeStream& stream, const void* otherObj)
{
    // Base
    Actor::Serialize(stream, otherObj);

    SERIALIZE_GET_OTHER_OBJ(UICanvas);

#if !COMPILE_WITHOUT_CSHARP
    stream.JKEY("V");
    void* params[1];
    params[0] = other ? other->GetOrCreateManagedInstance() : nullptr;
    MObject* exception = nullptr;
    auto method = other ? UICanvas_SerializeDiff : UICanvas_Serialize;
    auto invokeResultStr = (MonoString*)method->Invoke(GetOrCreateManagedInstance(), params, &exception);
    if (exception)
    {
        MException ex(exception);
        ex.Log(LogType::Error, TEXT("UICanvas::Serialize"));

        // Empty object
        stream.StartObject();
        stream.EndObject();
    }
    else
    {
        // Write result data
        auto invokeResultChars = mono_string_to_utf8(invokeResultStr);
        stream.RawValue(invokeResultChars);
        mono_free(invokeResultChars);
    }
#endif
}

void UICanvas::Deserialize(DeserializeStream& stream, ISerializeModifier* modifier)
{
    // Base
    Actor::Deserialize(stream, modifier);

#if !COMPILE_WITHOUT_CSHARP
    // Handle C# object data serialization
    const auto dataMember = stream.FindMember("V");
    if (dataMember != stream.MemberEnd())
    {
        rapidjson_flax::StringBuffer buffer;
        rapidjson_flax::Writer<rapidjson_flax::StringBuffer> writer(buffer);
        dataMember->value.Accept(writer);
        const auto str = buffer.GetString();
        void* args[1];
        args[0] = mono_string_new(mono_domain_get(), str);
        MObject* exception = nullptr;
        UICanvas_Deserialize->Invoke(GetOrCreateManagedInstance(), args, &exception);
        if (exception)
        {
            MException ex(exception);
            ex.Log(LogType::Error, TEXT("UICanvas::Deserialize"));
        }
    }
#endif

    if (IsDuringPlay())
    {
        UICANVAS_INVOKE(PostDeserialize);
    }
}

void UICanvas::BeginPlay(SceneBeginData* data)
{
    UICANVAS_INVOKE(PostDeserialize);

    // Base
    Actor::BeginPlay(data);
}

void UICanvas::EndPlay()
{
    UICANVAS_INVOKE(EndPlay);

    // Base
    Actor::EndPlay();
}

void UICanvas::OnParentChanged()
{
    // Base
    Actor::OnParentChanged();

    UICANVAS_INVOKE(ParentChanged);
}

void UICanvas::OnEnable()
{
    UICANVAS_INVOKE(Enable);

    // Base
    Actor::OnEnable();
}

void UICanvas::OnDisable()
{
    // Base
    Actor::OnDisable();

    UICANVAS_INVOKE(Disable);
}

void UICanvas::OnTransformChanged()
{
    // Base
    Actor::OnTransformChanged();

    _box = BoundingBox(_transform.Translation);
    _sphere = BoundingSphere(_transform.Translation, 0.0f);
}

#if USE_EDITOR

void UICanvas::OnActiveInTreeChanged()
{
    UICANVAS_INVOKE(OnActiveInTreeChanged);

    // Base
    Actor::OnActiveInTreeChanged();
}

#endif
