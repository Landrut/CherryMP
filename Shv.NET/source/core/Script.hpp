/**
 * Copyright (C) 2015 crosire
 *
 * This software is  provided 'as-is', without any express  or implied  warranty. In no event will the
 * authors be held liable for any damages arising from the use of this software.
 * Permission  is granted  to anyone  to use  this software  for  any  purpose,  including  commercial
 * applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *
 *   1. The origin of this software must not be misrepresented; you must not claim that you  wrote the
 *      original  software. If you use this  software  in a product, an  acknowledgment in the product
 *      documentation would be appreciated but is not required.
 *   2. Altered source versions must  be plainly  marked as such, and  must not be  misrepresented  as
 *      being the original software.
 *   3. This notice may not be removed or altered from any source distribution.
 */

#pragma once

namespace GTA
{
	#pragma region Forward Declarations
	ref class ScriptDomain;
	ref class ScriptSettings;
	#pragma endregion

	[System::AttributeUsage(System::AttributeTargets::Class, AllowMultiple = true)]
	public ref class RequireScript : System::Attribute
	{
	public:
		RequireScript(System::Type ^dependency) : _dependency(dependency) { }

	internal:
		System::Type ^_dependency;
	};

	public ref class Script abstract
	{
	public:
		Script();

		static void Wait(int ms);
		static void Yield();        

		event System::EventHandler ^Tick;
		event System::Windows::Forms::KeyEventHandler ^KeyUp;
		event System::Windows::Forms::KeyEventHandler ^KeyDown;
        event System::EventHandler ^Present;
		event System::EventHandler ^Aborted;

		property System::String ^Name
		{
			System::String ^get()
			{
				return GetType()->FullName;
			}
		}
		property System::String ^Filename
		{
			System::String ^get()
			{
				return _filename;
			}
		}
		property ScriptSettings ^Settings
		{
			ScriptSettings ^get();
		}

		void Abort();

		virtual System::String ^ToString() override
		{
			return Name;
		}

	protected:
		property int Interval
		{
			int get();
			void set(int value);
		}

        void AttachD3DHook();

	internal:
		void MainLoop();
        void D3DHook(void *swapchain);

		int _interval = 0;
		bool _running = false;
		System::String ^_filename;
		ScriptDomain ^_scriptdomain;
		System::Threading::Thread ^_thread;        
		System::Threading::AutoResetEvent ^_waitEvent = gcnew System::Threading::AutoResetEvent(false);
		System::Threading::AutoResetEvent ^_continueEvent = gcnew System::Threading::AutoResetEvent(false);
		System::Collections::Concurrent::ConcurrentQueue<System::Tuple<bool, System::Windows::Forms::KeyEventArgs ^> ^> ^_keyboardEvents = gcnew System::Collections::Concurrent::ConcurrentQueue<System::Tuple<bool, System::Windows::Forms::KeyEventArgs ^> ^>();
		ScriptSettings ^_settings;
	};
}