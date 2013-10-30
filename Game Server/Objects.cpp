#include "Objects.h"

using namespace game_server;
using namespace game_server::objects;

updatable::updatable() {
	
}

updatable::~updatable() {

}

base_obj::base_obj() {
	this->last_updated_by_cache = date_time::clock::now();
}

base_obj::~base_obj() {

}

map_obj::~map_obj() {

}

owned_obj::~owned_obj() {

}

map_owned_obj::~map_owned_obj() {

}
